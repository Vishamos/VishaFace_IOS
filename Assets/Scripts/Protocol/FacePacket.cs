using System;
using System.Buffers.Binary;

// 얼굴 프레임 → 바이너리 패킷 (얼굴 데이터 프로토콜 v1 §2, 사양 §5.3∼5.5).
// ARKit도 네트워크도 모른다. 숫자는 기기 바이트 순서와 상관없이 리틀 엔디언으로 쓴다.
public static class FacePacket
{
    public const int Size = 250;

    public const byte To = 11;       // Vishamos
    public const byte From = 22;     // Face iOS (등록 시그니처와 같아야 함)
    public const byte Type = 0x16;   // 얼굴 프레임
    public const byte Version = 1;
    public const byte FlagTracking = 0x01;

    // 오프셋 (프로토콜 §2 표)
    private const int OffsetFlags = 4;
    private const int OffsetSeq = 6;
    private const int OffsetTimeMs = 10;
    private const int OffsetBlendShapes = 14;
    private const int OffsetHeadRotation = 222;
    private const int OffsetHeadPosition = 238;

    public static byte[] Create(FaceFrame frame, uint seq, uint timeMs)
    {
        var buffer = new byte[Size];
        Write(buffer, frame, seq, timeMs);
        return buffer;
    }

    public static void Write(byte[] buffer, FaceFrame frame, uint seq, uint timeMs)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        if (buffer.Length < Size) throw new ArgumentException($"buffer는 {Size}바이트 이상이어야 한다", nameof(buffer));

        Span<byte> span = buffer.AsSpan(0, Size);
        span[0] = To;
        span[1] = From;
        span[2] = Type;
        span[3] = Version;
        span[OffsetFlags] = frame.Tracking ? FlagTracking : (byte)0;
        span[5] = 0; // reserved
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(OffsetSeq), seq);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(OffsetTimeMs), timeMs);

        for (int i = 0; i < FaceBlendShapes.Count; i++)
            WriteFloat(span, OffsetBlendShapes + i * 4, frame.BlendShapes[i]);

        var q = frame.HeadRotation;
        WriteFloat(span, OffsetHeadRotation, q.x);
        WriteFloat(span, OffsetHeadRotation + 4, q.y);
        WriteFloat(span, OffsetHeadRotation + 8, q.z);
        WriteFloat(span, OffsetHeadRotation + 12, q.w);

        var p = frame.HeadPosition;
        WriteFloat(span, OffsetHeadPosition, p.x);
        WriteFloat(span, OffsetHeadPosition + 4, p.y);
        WriteFloat(span, OffsetHeadPosition + 8, p.z);
    }

    private static void WriteFloat(Span<byte> span, int offset, float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), BitConverter.SingleToInt32Bits(value));
    }
}
