using UnityEngine;

// 한 프레임의 얼굴 데이터. 트래킹 계층이 채우고, 화면·패킷 계층이 읽는다.
// ARKit도 네트워크도 모른다. 값은 프레임마다 같은 객체를 덮어쓰므로, 받는 쪽은 이벤트 안에서 바로 읽고 끝낸다.
public sealed class FaceFrame
{
    // 지금 얼굴을 추적 중인지. false면 아래 값은 마지막으로 추적한 값이다 (프로토콜 §5).
    public bool Tracking;

    // 0∼1, 순서는 FaceBlendShapes.Names
    public readonly float[] BlendShapes = new float[FaceBlendShapes.Count];

    // 카메라 기준 머리 자세, Unity 좌표계 (프로토콜 §5a)
    public Quaternion HeadRotation = Quaternion.identity;
    public Vector3 HeadPosition;
}
