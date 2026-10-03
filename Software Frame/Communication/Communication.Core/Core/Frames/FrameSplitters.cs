// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：LengthPrefixedFrameSplitter、DelimiterFrameSplitter、FixedLengthFrameSplitter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Interfaces;

namespace Communication.Core.Frames
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

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TrySplit。
        /// 正文中的关键协作调用包括 Empty、ToArray；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 初始化 DelimiterFrameSplitter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DelimiterFrameSplitter(byte[] delimiter, bool includeDelimiter = true, int maxFrame = 65536)
        {
            _delimiter = delimiter;
            _includeDelimiter = includeDelimiter;
            _maxFrame = maxFrame;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TrySplit。
        /// 正文中的关键协作调用包括 Empty、IndexOf、ToArray；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IndexOf。
        /// </summary>
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

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TrySplit。
        /// 正文中的关键协作调用包括 Empty、Compute、SequenceEqual、AsSpan、ToArray；具体条件和先后次序见方法体。
        /// </summary>
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