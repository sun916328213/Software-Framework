// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：FrameBuilder、Crc16。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Interfaces;

namespace Communication.Core.Frames
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

        /// <summary>
        /// 初始化 FrameBuilder 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        private FrameBuilder() { }

        /// <summary>创建新的帧构建器</summary>
        public static FrameBuilder Create() => new();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Clear。
        /// 正文中的关键协作调用包括 SetLength；具体条件和先后次序见方法体。
        /// </summary>
        public IFrameBuilder Clear()
        {
            _stream.SetLength(0);
            return this;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendHeader。
        /// 正文中的关键协作调用包括 Write；具体条件和先后次序见方法体。
        /// </summary>
        public IFrameBuilder AppendHeader(byte[] header)
        {
            _stream.Write(header, 0, header.Length);
            return this;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendAddress。
        /// 正文中的关键协作调用包括 WriteByte；具体条件和先后次序见方法体。
        /// </summary>
        public IFrameBuilder AppendAddress(byte address)
        {
            _stream.WriteByte(address);
            return this;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendLength。
        /// 正文中的关键协作调用包括 Write；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendPayload。
        /// 正文中的关键协作调用包括 Write；具体条件和先后次序见方法体。
        /// </summary>
        public IFrameBuilder AppendPayload(byte[] payload)
        {
            _stream.Write(payload, 0, payload.Length);
            return this;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendChecksum。
        /// 正文中的关键协作调用包括 ToArray、ToUpperInvariant、Compute、Aggregate、Write；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendBytes。
        /// 正文中的关键协作调用包括 Write；具体条件和先后次序见方法体。
        /// </summary>
        public IFrameBuilder AppendBytes(byte[] bytes)
        {
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendDelimiter。
        /// 正文中的关键协作调用包括 Write；具体条件和先后次序见方法体。
        /// </summary>
        public IFrameBuilder AppendDelimiter(byte[] delimiter)
        {
            _stream.Write(delimiter, 0, delimiter.Length);
            return this;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Build。
        /// 正文中的关键协作调用包括 ToArray；具体条件和先后次序见方法体。
        /// </summary>
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