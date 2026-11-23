using UnityEngine;

// 트래킹 계층과 송신 계층을 잇는다 (worklist B3). 프레임마다 패킷을 만들어 Socket의 최신값 칸에 넣는다.
// - 등록된 동안에만 만든다. seq는 등록될 때마다 0부터 다시 센다 (프로토콜 §2).
// - 추적이 끊겨도 같은 빈도로 보낸다(FaceFrame이 마지막 값을 유지, 프로토콜 §5).
// - 패킷은 매번 새 배열이다. 송신 루프가 보내는 중인 배열을 덮어쓰지 않기 위해서다(250바이트 × 60회/초).
public class FacePacketSender : MonoBehaviour
{
    private FaceTracker tracker;
    private Socket socket;
    private uint seq;

    public static FacePacketSender Create(GameObject host, FaceTracker tracker, Socket socket)
    {
        var sender = host.AddComponent<FacePacketSender>();
        sender.tracker = tracker;
        sender.socket = socket;
        tracker.FrameUpdated += sender.OnFrameUpdated;
        socket.StateChanged += sender.OnStateChanged;
        return sender;
    }

    private void OnDestroy()
    {
        if (tracker != null) tracker.FrameUpdated -= OnFrameUpdated;
        if (socket != null) socket.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(Socket.SocketState state)
    {
        if (state == Socket.SocketState.Registered) seq = 0;
    }

    private void OnFrameUpdated(FaceFrame frame)
    {
        if (socket.State != Socket.SocketState.Registered) return;

        uint timeMs = (uint)(Time.realtimeSinceStartupAsDouble * 1000.0);
        socket.Send(FacePacket.Create(frame, seq, timeMs));
        seq++;
    }
}
