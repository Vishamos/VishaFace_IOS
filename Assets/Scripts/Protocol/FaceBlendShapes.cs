using System;

// 블렌드셰이프 이름 → 인덱스 고정 표 (얼굴 데이터 프로토콜 §4, 사양 §3.5).
// 이 순서가 패킷의 blendShapes 배열 순서다. 프로토콜 판(version)이 바뀌지 않는 한 바꾸지 않는다.
public static class FaceBlendShapes
{
    public const int Count = 52;

    public static readonly string[] Names =
    {
        "browDownLeft", "browDownRight", "browInnerUp", "browOuterUpLeft", "browOuterUpRight",
        "cheekPuff", "cheekSquintLeft", "cheekSquintRight",
        "eyeBlinkLeft", "eyeBlinkRight",
        "eyeLookDownLeft", "eyeLookDownRight", "eyeLookInLeft", "eyeLookInRight",
        "eyeLookOutLeft", "eyeLookOutRight", "eyeLookUpLeft", "eyeLookUpRight",
        "eyeSquintLeft", "eyeSquintRight", "eyeWideLeft", "eyeWideRight",
        "jawForward", "jawLeft", "jawOpen", "jawRight",
        "mouthClose", "mouthDimpleLeft", "mouthDimpleRight", "mouthFrownLeft", "mouthFrownRight",
        "mouthFunnel", "mouthLeft", "mouthLowerDownLeft", "mouthLowerDownRight",
        "mouthPressLeft", "mouthPressRight", "mouthPucker", "mouthRight",
        "mouthRollLower", "mouthRollUpper", "mouthShrugLower", "mouthShrugUpper",
        "mouthSmileLeft", "mouthSmileRight", "mouthStretchLeft", "mouthStretchRight",
        "mouthUpperUpLeft", "mouthUpperUpRight", "noseSneerLeft", "noseSneerRight",
        "tongueOut",
    };

    // 이름의 인덱스. 대소문자는 첫 글자만 무시한다(ARKit enum 이름 "JawOpen" → "jawOpen"). 없으면 -1.
    public static int IndexOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        string key = char.ToLowerInvariant(name[0]) + name.Substring(1);
        return Array.IndexOf(Names, key);
    }
}
