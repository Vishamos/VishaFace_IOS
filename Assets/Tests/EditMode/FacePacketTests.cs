using System;
using NUnit.Framework;
using UnityEngine;

// 얼굴 데이터 프로토콜 v1(docs/protocol/README.md)과 구현이 맞는지 확인한다.
// 기대값은 모두 문서에서 가져온다. 구현 코드를 보고 기대값을 정하지 않는다.
public class FacePacketTests
{
    // 문서 §2 바이트 배치
    const int DocSize = 250;
    const int OffTo = 0, OffFrom = 1, OffType = 2, OffVersion = 3, OffFlags = 4, OffReserved = 5;
    const int OffSeq = 6, OffTimeMs = 10, OffBlend = 14, OffRot = 222, OffPos = 238;

    // 문서 §4 표 (인덱스 = 배열 위치)
    static readonly string[] DocNames =
    {
        "browDownLeft", "browDownRight", "browInnerUp", "browOuterUpLeft", "browOuterUpRight",
        "cheekPuff", "cheekSquintLeft", "cheekSquintRight", "eyeBlinkLeft", "eyeBlinkRight",
        "eyeLookDownLeft", "eyeLookDownRight", "eyeLookInLeft", "eyeLookInRight", "eyeLookOutLeft",
        "eyeLookOutRight", "eyeLookUpLeft", "eyeLookUpRight", "eyeSquintLeft", "eyeSquintRight",
        "eyeWideLeft", "eyeWideRight", "jawForward", "jawLeft", "jawOpen",
        "jawRight", "mouthClose", "mouthDimpleLeft", "mouthDimpleRight", "mouthFrownLeft",
        "mouthFrownRight", "mouthFunnel", "mouthLeft", "mouthLowerDownLeft", "mouthLowerDownRight",
        "mouthPressLeft", "mouthPressRight", "mouthPucker", "mouthRight", "mouthRollLower",
        "mouthRollUpper", "mouthShrugLower", "mouthShrugUpper", "mouthSmileLeft", "mouthSmileRight",
        "mouthStretchLeft", "mouthStretchRight", "mouthUpperUpLeft", "mouthUpperUpRight", "noseSneerLeft",
        "noseSneerRight", "tongueOut",
    };

    // 정규화된 쿼터니언. 네 성분이 모두 달라 순서가 뒤바뀌면 드러난다. (1² + 2² + 4² + 10² = 11²)
    static readonly Quaternion TestRot = new Quaternion(1f / 11f, 2f / 11f, 4f / 11f, 10f / 11f);
    static readonly Vector3 TestPos = new Vector3(0.0123f, -0.0456f, -0.389f);

    // ---- 바이트 해석 도우미 (BitConverter를 쓰지 않고 리틀 엔디언으로 직접 읽는다) ----

    static uint ReadU32LE(byte[] b, int o)
    {
        return (uint)b[o] | ((uint)b[o + 1] << 8) | ((uint)b[o + 2] << 16) | ((uint)b[o + 3] << 24);
    }

    static float ReadF32LE(byte[] b, int o)
    {
        return BitConverter.Int32BitsToSingle((int)ReadU32LE(b, o));
    }

    // 블렌드셰이프마다 서로 다른 값을 넣은 프레임. 값은 float로 정확히 표현되는 수(i+1)/64.
    static FaceFrame MakeFrame(bool tracking)
    {
        var f = new FaceFrame();
        f.Tracking = tracking;
        for (int i = 0; i < f.BlendShapes.Length; i++)
            f.BlendShapes[i] = (i + 1) / 64f;
        f.HeadRotation = TestRot;
        f.HeadPosition = TestPos;
        return f;
    }

    // ---- 크기·상수 ----

    [Test]
    public void 크기는_250바이트()
    {
        Assert.AreEqual(DocSize, FacePacket.Size);
        Assert.AreEqual(DocSize, FacePacket.Create(MakeFrame(true), 0, 0).Length);
    }

    [Test]
    public void 공개_상수가_문서값과_같다()
    {
        Assert.AreEqual(11, FacePacket.To);
        Assert.AreEqual(22, FacePacket.From);
        Assert.AreEqual(0x16, FacePacket.Type);
        Assert.AreEqual(1, FacePacket.Version);
        Assert.AreEqual(0x01, FacePacket.FlagTracking);
        Assert.AreEqual(52, FaceBlendShapes.Count);
    }

    // ---- 헤더 ----

    [Test]
    public void 헤더_바이트()
    {
        byte[] b = FacePacket.Create(MakeFrame(true), 0, 0);
        Assert.AreEqual(11, b[OffTo], "to");
        Assert.AreEqual(22, b[OffFrom], "from");
        Assert.AreEqual(0x16, b[OffType], "type");
        Assert.AreEqual(1, b[OffVersion], "version");
        Assert.AreEqual(0, b[OffReserved], "reserved");
    }

    [Test]
    public void flags_추적중이면_1_나머지비트_0()
    {
        byte[] b = FacePacket.Create(MakeFrame(true), 0, 0);
        Assert.AreEqual(0x01, b[OffFlags]);
    }

    [Test]
    public void flags_추적끊김이면_0()
    {
        byte[] b = FacePacket.Create(MakeFrame(false), 0, 0);
        Assert.AreEqual(0x00, b[OffFlags]);
    }

    // 미리 쓰레기 값이 든 버퍼에 써도 reserved·flags 예약 비트가 0이 되는지 (버퍼가 0으로 비어 있다고 가정하면 안 된다)
    [Test]
    public void 쓰레기_버퍼에_써도_예약값은_0()
    {
        var buf = new byte[DocSize];
        for (int i = 0; i < buf.Length; i++) buf[i] = 0xFF;

        FacePacket.Write(buf, MakeFrame(false), 0, 0);
        Assert.AreEqual(0x00, buf[OffFlags], "flags (추적 끊김)");
        Assert.AreEqual(0, buf[OffReserved], "reserved");

        for (int i = 0; i < buf.Length; i++) buf[i] = 0xFF;
        FacePacket.Write(buf, MakeFrame(true), 0, 0);
        Assert.AreEqual(0x01, buf[OffFlags], "flags (추적 중)");
        Assert.AreEqual(0, buf[OffReserved], "reserved");
    }

    // ---- seq, timeMs ----

    [Test]
    public void seq와_timeMs_리틀엔디언()
    {
        byte[] b = FacePacket.Create(MakeFrame(true), 0x01020304u, 0xA1B2C3D4u);

        // seq: 오프셋 6, 낮은 바이트부터
        Assert.AreEqual(0x04, b[OffSeq + 0]);
        Assert.AreEqual(0x03, b[OffSeq + 1]);
        Assert.AreEqual(0x02, b[OffSeq + 2]);
        Assert.AreEqual(0x01, b[OffSeq + 3]);

        // timeMs: 오프셋 10, 최상위 비트가 켜진 값도 그대로
        Assert.AreEqual(0xD4, b[OffTimeMs + 0]);
        Assert.AreEqual(0xC3, b[OffTimeMs + 1]);
        Assert.AreEqual(0xB2, b[OffTimeMs + 2]);
        Assert.AreEqual(0xA1, b[OffTimeMs + 3]);

        Assert.AreEqual(0x01020304u, ReadU32LE(b, OffSeq));
        Assert.AreEqual(0xA1B2C3D4u, ReadU32LE(b, OffTimeMs));
    }

    [Test]
    public void seq와_timeMs_최대값()
    {
        byte[] b = FacePacket.Create(MakeFrame(true), uint.MaxValue, uint.MaxValue);
        Assert.AreEqual(uint.MaxValue, ReadU32LE(b, OffSeq));
        Assert.AreEqual(uint.MaxValue, ReadU32LE(b, OffTimeMs));
    }

    // ---- 블렌드셰이프 ----

    [Test]
    public void 블렌드셰이프_52개가_14부터_4바이트씩()
    {
        byte[] b = FacePacket.Create(MakeFrame(true), 0, 0);
        for (int i = 0; i < 52; i++)
            Assert.AreEqual((i + 1) / 64f, ReadF32LE(b, OffBlend + i * 4), "인덱스 " + i);
    }

    // float 바이트 자체가 IEEE 754 리틀 엔디언인지 (1.0f = 0x3F800000)
    [Test]
    public void 블렌드셰이프_float32_바이트_배치()
    {
        var f = MakeFrame(true);
        f.BlendShapes[24] = 1.0f; // jawOpen
        byte[] b = FacePacket.Create(f, 0, 0);
        int o = OffBlend + 24 * 4;
        Assert.AreEqual(0x00, b[o + 0]);
        Assert.AreEqual(0x00, b[o + 1]);
        Assert.AreEqual(0x80, b[o + 2]);
        Assert.AreEqual(0x3F, b[o + 3]);
    }

    [Test]
    public void 블렌드셰이프_이름표가_문서_4절과_같다()
    {
        Assert.AreEqual(52, FaceBlendShapes.Names.Length);
        for (int i = 0; i < 52; i++)
            Assert.AreEqual(DocNames[i], FaceBlendShapes.Names[i], "인덱스 " + i);
    }

    [Test]
    public void 블렌드셰이프_이름별_인덱스()
    {
        // 문서 §4 표의 대표 항목 (열 시작·끝, 예제에 나온 jawOpen 포함)
        Assert.AreEqual(0, FaceBlendShapes.IndexOf("browDownLeft"));
        Assert.AreEqual(8, FaceBlendShapes.IndexOf("eyeBlinkLeft"));
        Assert.AreEqual(9, FaceBlendShapes.IndexOf("eyeBlinkRight"));
        Assert.AreEqual(12, FaceBlendShapes.IndexOf("eyeLookInLeft"));
        Assert.AreEqual(13, FaceBlendShapes.IndexOf("eyeLookInRight"));
        Assert.AreEqual(24, FaceBlendShapes.IndexOf("jawOpen"));
        Assert.AreEqual(25, FaceBlendShapes.IndexOf("jawRight"));
        Assert.AreEqual(26, FaceBlendShapes.IndexOf("mouthClose"));
        Assert.AreEqual(38, FaceBlendShapes.IndexOf("mouthRight"));
        Assert.AreEqual(39, FaceBlendShapes.IndexOf("mouthRollLower"));
        Assert.AreEqual(43, FaceBlendShapes.IndexOf("mouthSmileLeft"));
        Assert.AreEqual(44, FaceBlendShapes.IndexOf("mouthSmileRight"));
        Assert.AreEqual(51, FaceBlendShapes.IndexOf("tongueOut"));

        // 전체 이름도 표의 인덱스와 일치
        for (int i = 0; i < 52; i++)
            Assert.AreEqual(i, FaceBlendShapes.IndexOf(DocNames[i]), DocNames[i]);
    }

    // IndexOf의 첫 글자 대소문자 무시·없는 이름은 문서가 아니라 공개 API 설명에 근거한 확인이다.
    [Test]
    public void IndexOf_첫글자_대문자와_없는이름()
    {
        Assert.AreEqual(24, FaceBlendShapes.IndexOf("JawOpen"));
        Assert.AreEqual(51, FaceBlendShapes.IndexOf("TongueOut"));
        Assert.AreEqual(-1, FaceBlendShapes.IndexOf("notAShape"));
    }

    // ---- 머리 회전·위치 ----

    [Test]
    public void 머리_회전_xyzw_222부터()
    {
        byte[] b = FacePacket.Create(MakeFrame(true), 0, 0);
        // 문서상 정규화된 값을 보낸다. 이미 정규화된 입력이므로 오차 범위 안에서 같아야 한다.
        const float tol = 1e-6f;
        Assert.AreEqual(TestRot.x, ReadF32LE(b, OffRot + 0), tol, "x");
        Assert.AreEqual(TestRot.y, ReadF32LE(b, OffRot + 4), tol, "y");
        Assert.AreEqual(TestRot.z, ReadF32LE(b, OffRot + 8), tol, "z");
        Assert.AreEqual(TestRot.w, ReadF32LE(b, OffRot + 12), tol, "w");
    }

    [Test]
    public void 머리_위치_xyz_238부터()
    {
        byte[] b = FacePacket.Create(MakeFrame(true), 0, 0);
        Assert.AreEqual(TestPos.x, ReadF32LE(b, OffPos + 0), "x");
        Assert.AreEqual(TestPos.y, ReadF32LE(b, OffPos + 4), "y");
        Assert.AreEqual(TestPos.z, ReadF32LE(b, OffPos + 8), "z");
    }

    // ---- 문서 §2 C# 수신 예를 그대로 실행 ----

    [Test]
    public void 문서_수신예로_읽으면_보낸값이_나온다()
    {
        var frame = MakeFrame(true);
        frame.BlendShapes[24] = 0.75f; // jawOpen
        byte[] data = FacePacket.Create(frame, 1234u, 5678u);

        // 아래는 README §2 예제와 같은 코드
        bool accepted = !(data.Length != 250 || data[2] != 0x16 || data[3] != 1);
        Assert.IsTrue(accepted, "수신 예의 형식 확인을 통과해야 한다");
        bool tracking = (data[4] & 0x01) != 0;
        uint seq = BitConverter.ToUInt32(data, 6);
        float jawOpen = BitConverter.ToSingle(data, 14 + 24 * 4);
        var rot = new Quaternion(
            BitConverter.ToSingle(data, 222), BitConverter.ToSingle(data, 226),
            BitConverter.ToSingle(data, 230), BitConverter.ToSingle(data, 234));
        var pos = new Vector3(
            BitConverter.ToSingle(data, 238), BitConverter.ToSingle(data, 242),
            BitConverter.ToSingle(data, 246));

        Assert.IsTrue(BitConverter.IsLittleEndian, "문서 전제: 수신 기기는 리틀 엔디언");
        Assert.IsTrue(tracking);
        Assert.AreEqual(1234u, seq);
        Assert.AreEqual(0.75f, jawOpen);
        Assert.AreEqual(TestRot.x, rot.x, 1e-6f);
        Assert.AreEqual(TestRot.y, rot.y, 1e-6f);
        Assert.AreEqual(TestRot.z, rot.z, 1e-6f);
        Assert.AreEqual(TestRot.w, rot.w, 1e-6f);
        Assert.AreEqual(TestPos.x, pos.x);
        Assert.AreEqual(TestPos.y, pos.y);
        Assert.AreEqual(TestPos.z, pos.z);
    }

    // ---- Write 인자 확인 ----

    [Test]
    public void Write_짧은버퍼는_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => FacePacket.Write(new byte[DocSize - 1], MakeFrame(true), 0, 0));
    }

    [Test]
    public void Write_null은_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => FacePacket.Write(null, MakeFrame(true), 0, 0));
        Assert.Throws<ArgumentNullException>(() => FacePacket.Write(new byte[DocSize], null, 0, 0));
    }

    [Test]
    public void Write_큰버퍼면_250바이트_뒤는_그대로()
    {
        var buf = new byte[DocSize + 16];
        for (int i = 0; i < buf.Length; i++) buf[i] = 0xEE;

        FacePacket.Write(buf, MakeFrame(true), 7u, 8u);

        for (int i = DocSize; i < buf.Length; i++)
            Assert.AreEqual(0xEE, buf[i], "오프셋 " + i);

        // 앞 250바이트는 Create 결과와 같아야 한다
        byte[] expected = FacePacket.Create(MakeFrame(true), 7u, 8u);
        for (int i = 0; i < DocSize; i++)
            Assert.AreEqual(expected[i], buf[i], "오프셋 " + i);
    }

    [Test]
    public void Write와_Create는_같은_바이트()
    {
        var buf = new byte[DocSize];
        FacePacket.Write(buf, MakeFrame(false), 0x01020304u, 0x05060708u);
        CollectionAssert.AreEqual(FacePacket.Create(MakeFrame(false), 0x01020304u, 0x05060708u), buf);
    }
}
