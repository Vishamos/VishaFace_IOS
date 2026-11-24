using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// VishaSynapse 중계 서버 송신 계층 (사양 §4.5, §4.6, §5.1∼5.8a, worklist B2)
// - ARKit을 모른다. 완성된 패킷(byte[])만 Send로 받는다.
// - 접속 → 등록(from = 22) → client.registered 수신 후에만 바이너리 송신.
// - 송신은 최신값 한 칸 + 단일 송신 루프 (60Hz 호출에도 송신이 겹치지 않음).
// - 수신 루프를 항상 돌린다 (ClientWebSocket은 ReceiveAsync 대기 중에만 pong을 보냄, 사양 §5.7a).
// - 접속 시작부터 10초 안에 등록되지 않으면 포기하고 재접속한다. 멈춘 작업은 기다리지 않는다(포기 신호와 경쟁).
// - 로그는 상태 변화·에러 코드만. 서버 주소·패킷 내용은 출력하지 않는다.
public class Socket : MonoBehaviour
{
    public enum SocketState { Disconnected, Connecting, Registered }

    // 서버 주소 저장 키 (iOS에서는 PlayerPrefs가 UserDefaults에 저장된다). 설정 화면(B1)이 같은 키를 쓴다.
    public const string HostKey = "Synapse.Host";
    public const string PortKey = "Synapse.Port";
    public const int DefaultPort = 4000;

    private const byte MySignature = 22;          // Face iOS
    private const float RegisterTimeoutSec = 10f; // 접속 시작 ∼ client.registered 수신 제한
    private const float RetryDelaySec = 3f;
    private const int ReceiveBufferSize = 65536;  // 서버 한도 64KB

    public SocketState State { get; private set; } = SocketState.Disconnected;
    public int LastErrorCode { get; private set; } // 서버 error JSON의 code, 또는 비정상 종료 코드(1008 등). 0 = 없음
    public event Action<SocketState> StateChanged;

    private byte[] pending;          // 최신값 한 칸
    private bool skipRetryDelay;     // 재접속 요청 시 대기 없이 재접속
    private CancellationTokenSource retryDelayCts; // 재접속 대기 중일 때만 존재

    // 지금 연결 시도의 포기 신호. 값은 포기 이유(로그용).
    // iOS에서 ConnectAsync·ReceiveAsync가 취소 토큰과 Abort()를 무시하고 멈추는 경우가 있어서(worklist B2 버그),
    // 연결 작업을 기다리지 않고 이 신호와 경쟁시켜 먼저 오는 쪽을 따른다.
    private TaskCompletionSource<string> abandon;

    // 연결 세대. 연결이 끝나거나 버려지면 올린다. 버려진 연결이 뒤늦게 응답해도 상태를 바꾸지 못하게 한다.
    private int generation;

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
        GiveUp(abandon, "재접속 요청");
        retryDelayCts?.Cancel();
    }

    // 포기 신호를 보내고 그 자리에서 세대를 올린다.
    // (신호를 받은 쪽이 깨어나기 전에 버려진 연결이 client.registered를 받아도 상태를 바꾸지 못하게)
    private void GiveUp(TaskCompletionSource<string> signal, string reason)
    {
        if (signal != null && signal == abandon && signal.TrySetResult(reason))
            generation++;
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

    // 연결 하나: 접속 → 등록 → 수신. 연결 작업(session)과 포기 신호 중 먼저 끝나는 쪽을 따른다.
    private async Awaitable ConnectOnceAsync(Uri uri)
    {
        int gen = generation;
        var ws = new ClientWebSocket();
        var giveUp = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        abandon = giveUp;
        Interlocked.Exchange(ref pending, null); // 이전 연결 때의 오래된 값은 버린다
        SetState(SocketState.Connecting);

        // 버려진 연결이 아직 토큰을 쓸 수 있으므로 Dispose하지 않는다(GC에 맡김).
        var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        using (destroyCancellationToken.Register(() => giveUp.TrySetResult("종료")))
        {
            _ = RegisterWatchdogAsync(gen, giveUp, connectionCts.Token);

            Task session = RunSessionAsync(ws, uri, gen, connectionCts.Token);
            Task first = await Task.WhenAny(session, giveUp.Task);

            connectionCts.Cancel(); // 감시 타이머·송수신 정지 요청
            if (first == giveUp.Task)
            {
                if (!destroyCancellationToken.IsCancellationRequested)
                    Debug.LogWarning($"[Socket] 연결 포기: {giveUp.Task.Result}");
                // 멈춘 작업은 기다리지 않는다. 끊기만 하고, 나중에 끝나면 정리한다.
                try { ws.Abort(); } catch (Exception) { /* 이미 닫힘 */ }
                _ = session.ContinueWith(_ => ws.Dispose(), TaskScheduler.Default);
            }
            else
            {
                ws.Dispose();
            }
        }

        if (generation == gen) generation++; // 이 연결을 무효로 만든다 (포기했으면 이미 올라가 있음)
        if (abandon == giveUp) abandon = null;
        SetState(SocketState.Disconnected);
    }

    // 접속 시작부터 RegisterTimeoutSec 안에 등록되지 않으면 포기 신호를 보낸다.
    private async Awaitable RegisterWatchdogAsync(int gen, TaskCompletionSource<string> giveUp, CancellationToken token)
    {
        try
        {
            await Awaitable.WaitForSecondsAsync(RegisterTimeoutSec, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (gen == generation && State != SocketState.Registered)
            GiveUp(giveUp, "등록 시간 초과");
    }

    // 접속 → 등록 메시지 → 수신 루프. 예외는 여기서 처리하고 밖으로 내보내지 않는다.
    private async Task RunSessionAsync(ClientWebSocket ws, Uri uri, int gen, CancellationToken token)
    {
        // 수신 루프가 끝나면 취소해 송신 루프도 멈춘다.
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        Awaitable sendLoop = null;
        try
        {
            await ws.ConnectAsync(uri, sessionCts.Token);

            string json = JsonUtility.ToJson(new RegisterMessage
            {
                to = 0, from = MySignature, type = "client.register", data = ""
            });
            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                WebSocketMessageType.Text, true, sessionCts.Token);

            sendLoop = SendLoopAsync(ws, gen, sessionCts.Token);
            await ReceiveLoopAsync(ws, gen, sessionCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 포기·종료로 취소됨: 이유는 ConnectOnceAsync가 기록한다
        }
        catch (Exception e)
        {
            if (gen == generation) Debug.LogWarning($"[Socket] 연결 끊김: {e.GetType().Name}");
        }
        finally
        {
            sessionCts.Cancel();
            if (sendLoop != null)
            {
                try { await sendLoop; }
                catch (Exception) { /* 송신 루프는 연결 종료와 함께 끝난다 */ }
            }
        }
    }

    private async Awaitable SendLoopAsync(ClientWebSocket ws, int gen, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Awaitable.NextFrameAsync(token);
            if (gen != generation || State != SocketState.Registered) continue;

            byte[] packet = Interlocked.Exchange(ref pending, null);
            if (packet == null) continue;

            await ws.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, token);
        }
    }

    private async Awaitable ReceiveLoopAsync(ClientWebSocket ws, int gen, CancellationToken token)
    {
        byte[] buffer = new byte[ReceiveBufferSize];
        while (ws.State == WebSocketState.Open)
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
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), token);
                offset += result.Count;
            } while (!result.EndOfMessage);

            if (gen != generation) return; // 버려진 연결: 상태를 바꾸지 않는다

            if (result.MessageType == WebSocketMessageType.Close)
            {
                var status = ws.CloseStatus;
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
