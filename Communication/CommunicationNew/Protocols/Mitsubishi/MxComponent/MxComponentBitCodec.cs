using System;

namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>
/// 在 MX Component 的 16 点位字表示与 MC 二进制半字节表示之间转换。
/// 该纯逻辑模块由直接模式和 net48 桥接模式共同使用，不依赖 COM。
/// </summary>
internal static class MxComponentBitCodec
{
    /// <summary>计算满足 MX 块读取对齐要求的起点、位偏移和字数。</summary>
    public static ReadWindow CreateReadWindow(int start, int quantity)
    {
        if (start is < 0 or > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        if (quantity <= 0 || (long)start + quantity - 1 > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var alignedStart = start / 16 * 16;
        var bitOffset = start - alignedStart;
        var wordCount = checked((bitOffset + quantity + 15) / 16);
        return new ReadWindow(alignedStart, bitOffset, wordCount);
    }

    /// <summary>将 MX 返回的低位优先字数组转换为 MC 偶数点在高半字节的响应格式。</summary>
    public static byte[] PackReadWords(short[] words, int bitOffset, int quantity)
    {
        if (words is null)
        {
            throw new ArgumentNullException(nameof(words));
        }
        if (bitOffset is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(bitOffset));
        }
        if (quantity <= 0 || bitOffset + quantity > words.Length * 16)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var payload = new byte[(quantity + 1) / 2];
        for (var index = 0; index < quantity; index++)
        {
            var sourceIndex = bitOffset + index;
            if ((words[sourceIndex / 16] & (1 << (sourceIndex % 16))) != 0)
            {
                payload[index / 2] |= index % 2 == 0 ? (byte)0x10 : (byte)0x01;
            }
        }
        return payload;
    }

    /// <summary>从 MC 半字节位负载中读取一个供 SetDevice2 使用的 0/1 值。</summary>
    public static short GetWriteValue(byte[] payload, int index)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }
        if (index < 0 || index >= payload.Length * 2)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var packed = payload[index / 2];
        return checked((short)(index % 2 == 0 ? (packed >> 4) & 0x01 : packed & 0x01));
    }

    /// <summary>MX 位块读取窗口。</summary>
    internal readonly struct ReadWindow
    {
        public ReadWindow(int alignedStart, int bitOffset, int wordCount)
        {
            AlignedStart = alignedStart;
            BitOffset = bitOffset;
            WordCount = wordCount;
        }

        public int AlignedStart { get; }
        public int BitOffset { get; }
        public int WordCount { get; }
    }
}
