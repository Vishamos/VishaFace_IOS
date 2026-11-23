using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;

// VishaSynapse 중계 서버 송신 계층 (사양 §4.5, §4.6, §5.1∼5.8a, worklist B2)
// - ARKit을 모른다. 완성된 패킷(byte[])만 Send로 받는다.
// - 접속 → 등록(from = 22) → client.registered 수신 후에만 바이너리 송신.
// - 송신은 최신값 한 칸 + 단일 송신 루프 (60Hz 호출에도 송신이 겹치지 않음).
// - 수신 루프를 항상 돌린다 (ClientWebSocket은 ReceiveAsync 대기 중에만 pong을 보냄, 사양 §5.7a).
// - 로그는 상태 변화·에러 코드만. 서버 주소·패킷 내용은 출력하지 않는다.
public class Socket : MonoBehaviour
{
    public enum SocketState { Disconnected, Connecting, Registered }

    // 서버 주소 저장 키 (iOS에서는 PlayerPrefs가 UserDefaults에 저장된다). 설정 화면(B1)이 같은 키를 쓴다.
    public const string HostKey = "Synapse.Host";
    public const string PortKey = "Synapse.Port";
    public const int DefaultPort = 4000;

    private const byte MySignature = 22;          // Face iOS
    private const int ConnectTimeoutMs = 5000;
    private const float RetryDelaySec = 3f;
    private const int ReceiveBufferSize = 65536;  // 서버 한도 64KB

    public SocketState State { get; private set; } = SocketState.Disconnected;
    public int LastErrorCode { get; private set; } // 서버 error JSON의 code, 또는 비정상 종료 코드(1008 등). 0 = 없음
    public event Action<SocketState> StateChanged;

    private ClientWebSocket client;
    private byte[] pending;          // 최신값 한 칸
    private bool skipRetryDelay;     // 재접속 요청 시 대기 없이 재접속
    private CancellationTokenSource retryDelayCts; // 재접속 대기 중일 때만 존재

    // 패킷을 최신값 칸에 넣는다. 등록 전에 넣은 값은 송신되지 않고 다음 값으로 덮인다.
    public void Send(byte[] packet)
    {
        Interlocked.Exchange(ref pending, packet);
    }

    // 지금 연결(또는 재접속 대기)을 끊고 저장된 주소로 바로 다시 접속한다.
    // 설정 화면에서 주소를 바꿨을 때, 앱이 다시 화면에 돌아왔을 때 쓴다.
    public void Reconnect()
    {
        skipRetryDelay = true;
        client?.Abort();
        retryDelayCts?.Cancel();
    }

    private void Start()
    {
        _ = RunAsync();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) return;
        // 복귀 시 기존 연결이 죽어 있을 수 있으므로 끊고 바로 다시 접속한다 (사양 §5.8a).
        Reconnect();
    }

    private async Awaitable RunAsync()
    {
        while (!destroyCancellationToken.IsCancellationRequested)
        {
            if (TryGetServerUri(out Uri uri))
            {
                await ConnectOnceAsync(uri);
                if (destroyCancellationToken.IsCancellationRequested) return;
            }

            if (skipRetryDelay)
            {
                skipRetryDelay = false;
                continue;
            }

            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            retryDelayCts = delayCts;
            try
            {
                await Awaitable.WaitForSecondsAsync(RetryDelaySec, delayCts.Token);
            }
            catch (OperationCanceledException) when (destroyCancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                skipRetryDelay = false; // Reconnect 요청으로 대기를 끝냄
            }
            finally
            {
                retryDelayCts = null;
            }
        }
    }

    private async Awaitable ConnectOnceAsync(Uri uri)
    {
        client = new ClientWebSocket();
        Interlocked.Exchange(ref pending, null); // 이전 연결 때의 오래된 값은 버린다
        SetState(SocketState.Connecting);

        // 이 연결 하나의 수명. 수신 루프가 끝나면 취소해 송신 루프도 멈춘다.
        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        Awaitable sendLoop = null;

        try
        {
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken))
            {
                timeoutCts.CancelAfter(ConnectTimeoutMs);
                await client.ConnectAsync(uri, timeoutCts.Token);
            }

            string json = JsonUtility.ToJson(new RegisterMessage
            {
                to = 0, from = MySignature, type = "client.register", data = ""
            });
            await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                WebSocketMessageType.Text, true, connectionCts.Token);

            sendLoop = SendLoopAsync(connectionCts.Token);
            await ReceiveLoopAsync(connectionCts.Token);
        }
        catch (OperationCanceledException) when (destroyCancellationToken.IsCancellationRequested)
        {
            // 오브젝트 파괴: 조용히 종료
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[Socket] 접속 시간 초과");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Socket] 연결 끊김: {e.GetType().Name}");
        }
        finally
        {
            connectionCts.Cancel();
            if (sendLoop != null)
            {
                try { await sendLoop; }
                catch (Exception) { /* 송신 루프는 연결 종료와 함께 끝난다 */ }
            }
            client.Dispose();
            client = null;
            SetState(SocketState.Disconnected);
        }
    }

    private async Awaitable SendLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Awaitable.NextFrameAsync(token);
            if (State != SocketState.Registered) continue;

            byte[] packet = Interlocked.Exchange(ref pending, null);
            if (packet == null) continue;

            await client.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, token);
        }
    }

    private async Awaitable ReceiveLoopAsync(CancellationToken token)
    {
        byte[] buffer = new byte[ReceiveBufferSize];
        while (client.State == WebSocketState.Open)
        {
            int offset = 0;
            bool overflow = false;
            WebSocketReceiveResult result;
            do
            {
                if (offset == buffer.Length)
                {
                    // 버퍼보다 큰 메시지: 나머지를 읽어 버리고 이 메시지는 처리하지 않는다.
                    overflow = true;
                    offset = 0;
                }
                result = await client.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), token);
                offset += result.Count;
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                var status = client.CloseStatus;
                if (status.HasValue && status.Value != WebSocketCloseStatus.NormalClosure)
                {
                    LastErrorCode = (int)status.Value;
                    Debug.LogWarning($"[Socket] 서버가 연결을 닫음: {LastErrorCode}");
                }
                break;
            }

            if (overflow)
            {
                Debug.LogWarning("[Socket] 너무 큰 메시지를 버림");
                continue;
            }

            // 이 앱은 받을 바이너리 데이터가 없다. 텍스트(JSON)만 처리한다.
            if (result.MessageType == WebSocketMessageType.Text)
                HandleText(Encoding.UTF8.GetString(buffer, 0, offset));
        }
    }

    private void HandleText(string json)
    {
        Envelope envelope;
        try
        {
            envelope = JsonUtility.FromJson<Envelope>(json);
        }
        catch (Exception)
        {
            Debug.LogWarning("[Socket] 해석할 수 없는 JSON 수신");
            return;
        }
        if (envelope == null) return;

        switch (envelope.type)
        {
            case "client.registered":
                LastErrorCode = 0;
                SetState(SocketState.Registered);
                break;
            case "error":
                var error = JsonUtility.FromJson<ErrorMessage>(json);
                LastErrorCode = error?.data?.code ?? 0;
                Debug.LogWarning($"[Socket] 서버 에러: {LastErrorCode}");
                break;
        }
    }

    private static bool TryGetServerUri(out Uri uri)
    {
        uri = null;
        string host = PlayerPrefs.GetString(HostKey, "");
        int port = PlayerPrefs.GetInt(PortKey, DefaultPort);
        if (string.IsNullOrWhiteSpace(host)) return false; // 설정 전
        return Uri.TryCreate($"ws://{host.Trim()}:{port}/", UriKind.Absolute, out uri);
    }

    private void SetState(SocketState next)
    {
        if (State == next) return;
        State = next;
        Debug.Log($"[Socket] 상태: {next}");
        StateChanged?.Invoke(next);
    }

    // ── Synapse JSON 메시지 (키는 소문자, Synapse 프로토콜 §4.1) ──

    [Serializable]
    private class RegisterMessage
    {
        public int to;
        public int from;
        public string type;
        public string data;
    }

    // type만 먼저 읽는다 (data 모양이 메시지마다 다름).
    [Serializable]
    private class Envelope
    {
        public string type;
    }

    [Serializable]
    private class ErrorMessage
    {
        public string type;
        public ErrorData data;
    }

    [Serializable]
    private class ErrorData
    {
        public int code;
        public string message;
    }
}
