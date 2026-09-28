using CommunicationNew.Core.Interfaces;

namespace CommunicationNew.Core.Frames
{
    /// <summary>
    /// 按长度前缀拆分帧：前 N 字节表示载荷长度（大端/小端可配）
    /// </summary>
    public class LengthPrefixedFrameSplitter : IFrameSplitter
    {
        private readonly int _lengthFieldSize;
        private readonly bool _bigEndian;
        private readonly int _prefix;
        private readonly int _maxFrame;

        /// <param name="lengthFieldSize">长度字段字节数（默认 2）</param>
        /// <param name="bigEndian">长度字段是否大端（默认 true）</param>
        /// <param name="prefix">长度字段后的固定前缀字节数（默认 0）</param>
        /// <param name="maxFrame">单帧最大字节数，防止错误解析内存膨胀（默认 64KB）</param>
        public LengthPrefixedFrameSplitter(int lengthFieldSize = 2, bool bigEndian = true, int prefix = 0, int maxFrame = 65536)
        {
            _lengthFieldSize = lengthFieldSize;
            _bigEndian = bigEndian;
            _prefix = prefix;
            _maxFrame = maxFrame;
        }

        public bool TrySplit(ReadOnlySpan<byte> buffer, out byte[] frame, out int consumed)
        {
            frame = Array.Empty<byte>();
            consumed = 0;

            if (buffer.Length < _lengthFieldSize) return false;
            int length = 0;
            for (int i = 0; i < _lengthFieldSize; i++)
            {
                var idx = _bigEndian ? i : _lengthFieldSize - 1 - i;
                length = (length << 8) | buffer[idx];
            }

            var total = _lengthFieldSize + _prefix + length;
            if (total > _maxFrame) throw new InvalidDataException($"帧长度 {total} 超过上限 {_maxFrame}，可能帧解析错位");
            if (buffer.Length < total) return false;

            frame = buffer[..total].ToArray();
            consumed = total;
            return true;
        }
    }

    /// <summary>
    /// 按固定结束符拆分帧（分包时优先校验 CRC 提高可靠性）
    /// </summary>
    public class DelimiterFrameSplitter : IFrameSplitter
    {
        private readonly byte[] _delimiter;
        private readonly bool _includeDelimiter;
        private readonly int _maxFrame;

        public DelimiterFrameSplitter(byte[] delimiter, bool includeDelimiter = true, int maxFrame = 65536)
        {
            _delimiter = delimiter;
            _includeDelimiter = includeDelimiter;
            _maxFrame = maxFrame;
        }

        public bool TrySplit(ReadOnlySpan<byte> buffer, out byte[] frame, out int consumed)
        {
            frame = Array.Empty<byte>();
            consumed = 0;

            var end = IndexOf(buffer, _delimiter);
            if (end < 0)
            {
                if (buffer.Length > _maxFrame) throw new InvalidDataException("未找到结束符且缓冲超限");
                return false;
            }

            var length = end + _delimiter.Length;
            frame = buffer[..(_includeDelimiter ? length : end)].ToArray();
            consumed = length;
            return true;
        }

        private static int IndexOf(ReadOnlySpan<byte> src, byte[] pattern)
        {
            if (pattern.Length == 0) return -1;
            for (int i = 0; i <= src.Length - pattern.Length; i++)
            {
                var found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (src[i + j] != pattern[j]) { found = false; break; }
                }
                if (found) return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// 按固定帧长拆分（定长帧）
    /// </summary>
    public class FixedLengthFrameSplitter : IFrameSplitter
    {
        private readonly int _frameLength;
        private readonly bool _validateCrc;

        /// <param name="frameLength">固定帧长</param>
        /// <param name="validateCrc">若帧尾含 2 字节 CRC16 是否校验（默认 false）</param>
        public FixedLengthFrameSplitter(int frameLength, bool validateCrc = false)
        {
            _frameLength = frameLength;
            _validateCrc = validateCrc;
        }

        public bool TrySplit(ReadOnlySpan<byte> buffer, out byte[] frame, out int consumed)
        {
            frame = Array.Empty<byte>();
            consumed = 0;
            if (buffer.Length < _frameLength) return false;

            var candidate = buffer[.._frameLength];
            if (_validateCrc)
            {
                var crc = Crc16.Compute(candidate[..^2]);
                if (!crc.AsSpan().SequenceEqual(candidate[^2..])) return false;
            }

            frame = candidate.ToArray();
            consumed = _frameLength;
            return true;
        }
    }
}