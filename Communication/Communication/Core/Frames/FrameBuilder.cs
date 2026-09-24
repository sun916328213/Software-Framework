using CommunicationNew.Core.Interfaces;

namespace CommunicationNew.Core.Frames
{
    /// <summary>
    /// 流式帧构建器 —— 模块化组装协议数据帧（可链式调用）。
    /// 例：
    ///   var frame = FrameBuilder.Create()
    ///       .AppendHeader(new byte[]{0x02})
    ///       .AppendAddress(0x01)
    ///       .AppendLength(4)
    ///       .AppendPayload(new byte[]{0x12,0x34})
    ///       .AppendChecksum("CRC16")
    ///       .AppendDelimiter(new byte[]{0x03})
    ///       .Build();
    /// </summary>
    public class FrameBuilder : IFrameBuilder
    {
        private readonly MemoryStream _stream = new();

        private FrameBuilder() { }

        /// <summary>创建新的帧构建器</summary>
        public static FrameBuilder Create() => new();

        public IFrameBuilder Clear()
        {
            _stream.SetLength(0);
            return this;
        }

        public IFrameBuilder AppendHeader(byte[] header)
        {
            _stream.Write(header, 0, header.Length);
            return this;
        }

        public IFrameBuilder AppendAddress(byte address)
        {
            _stream.WriteByte(address);
            return this;
        }

        public IFrameBuilder AppendLength(int length, int fieldSize = 2, bool bigEndian = true)
        {
            var bytes = new byte[fieldSize];
            for (int i = 0; i < fieldSize; i++)
            {
                var idx = bigEndian ? fieldSize - 1 - i : i;
                bytes[idx] = (byte)(length >> (8 * i));
            }
            _stream.Write(bytes, 0, fieldSize);
            return this;
        }

        public IFrameBuilder AppendPayload(byte[] payload)
        {
            _stream.Write(payload, 0, payload.Length);
            return this;
        }

        public IFrameBuilder AppendChecksum(string algorithm = "CRC16")
        {
            var bytes = _stream.ToArray();
            byte[] checksum = algorithm.ToUpperInvariant() switch
            {
                "CRC16" => Crc16.Compute(bytes),
                "SUM" => new[] { bytes.Aggregate((byte)0, (a, b) => (byte)(a + b)) },
                _ => throw new NotSupportedException($"校验算法 '{algorithm}' 不受支持"),
            };
            _stream.Write(checksum, 0, checksum.Length);
            return this;
        }

        public IFrameBuilder AppendBytes(byte[] bytes)
        {
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        public IFrameBuilder AppendDelimiter(byte[] delimiter)
        {
            _stream.Write(delimiter, 0, delimiter.Length);
            return this;
        }

        public byte[] Build() => _stream.ToArray();
    }

    /// <summary>
    /// CRC16 计算（Modbus 多项式 0xA001）
    /// </summary>
    public static class Crc16
    {
        /// <summary>计算 CRC16（Modbus 标准）</summary>
        public static byte[] Compute(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            foreach (var b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc & 0x0001) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
            }
            return new[] { (byte)(crc & 0xFF), (byte)(crc >> 8) }; // 低字节在前
        }
    }
}