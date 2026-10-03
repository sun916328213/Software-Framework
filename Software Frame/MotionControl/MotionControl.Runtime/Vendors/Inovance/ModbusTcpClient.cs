// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：ModbusTcpClient。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net.Sockets。导入不代表所有类型都会在运行时加载。
using System.Net.Sockets;

namespace MotionControl.Runtime.Vendors.Inovance
{
    /// <summary>
    /// 轻量Modbus TCP主站客户端（标准MBAP协议，FC03/06/10）
    /// <para>用于与汇川H3U/H5U/Easy/AM系列控制器网口通讯，避免引入额外依赖</para>
    /// </summary>
    internal sealed class ModbusTcpClient : IDisposable
    {
        private const int MbapHeaderLength = 7;
        private const int MaximumMbapLength = 254;
        private readonly object _sync = new();
        private TcpClient? _client;
        private Stream? _stream;
        private ushort _txId;
        private int _timeoutMs = 2000;

        /// <summary>
        /// 初始化 ModbusTcpClient 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ModbusTcpClient() { }

        /// <summary>
        /// 初始化 ModbusTcpClient 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 ThrowIfNull、nameof；具体条件和先后次序见方法体。
        /// </summary>
        internal ModbusTcpClient(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanRead || !stream.CanWrite)
                throw new ArgumentException("Modbus 字节流必须同时支持读取和写入。", nameof(stream));
            _stream = stream;
        }

        /// <summary>连接超时时间（毫秒）</summary>
        public int TimeoutMs
        {
            get => _timeoutMs;
            set => _timeoutMs = value > 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), "超时时间必须大于 0。");
        }

        /// <summary>
        /// 建立TCP连接
        /// </summary>
        public bool Connect(string ip, int port)
        {
            lock (_sync)
            {
                try
                {
                    CloseConnection();
                    _client = new TcpClient();
                    var task = _client.ConnectAsync(ip, port);
                    if (!task.Wait(TimeoutMs) || !_client.Connected)
                    {
                        CloseConnection();
                        return false;
                    }
                    _client.ReceiveTimeout = TimeoutMs;
                    _client.SendTimeout = TimeoutMs;
                    _stream = _client.GetStream();
                    return true;
                }
                catch
                {
                    CloseConnection();
                    return false;
                }
            }
        }

        /// <summary>是否已连接</summary>
        public bool IsConnected
        {
            get
            {
                lock (_sync)
                    return _stream != null && (_client?.Connected ?? true);
            }
        }

        /// <summary>
        /// 读保持寄存器（FC03）
        /// </summary>
        /// <param name="slaveId">从站号</param>
        /// <param name="address">寄存器地址（0基）</param>
        /// <param name="count">寄存器数量</param>
        /// <returns>寄存器值数组，失败返回null</returns>
        public short[]? ReadHoldingRegisters(byte slaveId, ushort address, ushort count)
        {
            if (count is 0 or > 125)
                return null;

            lock (_sync)
            {
                try
                {
                    var request = BuildRequest(slaveId, 0x03);
                    request.AddRange(new byte[] { (byte)(address >> 8), (byte)address, (byte)(count >> 8), (byte)count });

                    var response = Transact(request.ToArray());
                    var byteCount = count * 2;
                    if (response == null || response[7] != 0x03 || response.Length != 9 + byteCount ||
                        response[8] != byteCount)
                        return null;
                    var result = new short[count];
                    for (int i = 0; i < count; i++)
                        result[i] = (short)((response[9 + i * 2] << 8) | response[10 + i * 2]);
                    return result;
                }
                catch
                {
                    CloseConnection();
                    return null;
                }
            }
        }

        /// <summary>
        /// 写单个寄存器（FC06）
        /// </summary>
        public bool WriteSingleRegister(byte slaveId, ushort address, short value)
        {
            lock (_sync)
            {
                try
                {
                    var request = BuildRequest(slaveId, 0x06);
                    request.AddRange(new byte[] { (byte)(address >> 8), (byte)address, (byte)(value >> 8), (byte)value });
                    var bytes = request.ToArray();
                    var response = Transact(bytes);
                    return response is { Length: 12 } && response[7] == 0x06 &&
                        response.AsSpan(8, 4).SequenceEqual(bytes.AsSpan(8, 4));
                }
                catch
                {
                    CloseConnection();
                    return false;
                }
            }
        }

        /// <summary>
        /// 写多个寄存器（FC10）
        /// </summary>
        public bool WriteMultipleRegisters(byte slaveId, ushort address, short[] values)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length is 0 or > 123)
                return false;

            lock (_sync)
            {
                try
                {
                    int byteCount = values.Length * 2;
                    var request = BuildRequest(slaveId, 0x10);
                    request.AddRange(new byte[]
                    {
                        (byte)(address >> 8), (byte)address,
                        (byte)(values.Length >> 8), (byte)values.Length,
                        (byte)byteCount,
                    });
                    foreach (var v in values)
                        request.AddRange(new byte[] { (byte)(v >> 8), (byte)v });
                    var bytes = request.ToArray();
                    var response = Transact(bytes);
                    return response is { Length: 12 } && response[7] == 0x10 &&
                        response.AsSpan(8, 4).SequenceEqual(bytes.AsSpan(8, 4));
                }
                catch
                {
                    CloseConnection();
                    return false;
                }
            }
        }

        /// <summary>构造MBAP请求头（事务ID+协议号+后续填充）</summary>
        private List<byte> BuildRequest(byte slaveId, byte functionCode)
        {
            _txId++;
            return new List<byte>
            {
                (byte)(_txId >> 8), (byte)_txId,   // 事务标识
                0x00, 0x00,                        // 协议标识=Modbus
                0x00, 0x00,                        // 长度（占位，发送前修正）
                slaveId, functionCode,
            };
        }

        /// <summary>发送请求并根据MBAP长度域接收、校验完整响应。</summary>
        private byte[]? Transact(byte[] request)
        {
            if (_stream == null || !_stream.CanWrite)
                return null;
            // 修正长度域 = UnitId + Func + 数据字节数
            int len = request.Length - 6;
            request[4] = (byte)(len >> 8);
            request[5] = (byte)len;

            _stream.Write(request, 0, request.Length);

            var header = ReadExact(_stream, MbapHeaderLength);
            if (header == null)
                return CloseMalformedConnection();

            int responseLength = (header[4] << 8) | header[5];
            if (responseLength is < 2 or > MaximumMbapLength ||
                header[0] != request[0] || header[1] != request[1] ||
                header[2] != 0 || header[3] != 0 || header[6] != request[6])
                return CloseMalformedConnection();

            var response = new byte[6 + responseLength];
            Buffer.BlockCopy(header, 0, response, 0, header.Length);
            var body = ReadExact(_stream, responseLength - 1);
            if (body == null)
                return CloseMalformedConnection();
            Buffer.BlockCopy(body, 0, response, MbapHeaderLength, body.Length);

            var responseFunction = response[7];
            if (responseFunction != request[7] && responseFunction != (request[7] | 0x80))
                return CloseMalformedConnection();
            return response;
        }

        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadExact。
        /// 正文中的关键协作调用包括 Read；具体条件和先后次序见方法体。
        /// </summary>
        private static byte[]? ReadExact(Stream stream, int count)
        {
            var buffer = new byte[count];
            var received = 0;
            while (received < count)
            {
                int n = stream.Read(buffer, received, count - received);
                if (n <= 0)
                    return null;
                received += n;
            }
            return buffer;
        }

        /// <summary>
        /// 执行关闭步骤；对应入口为 CloseMalformedConnection。
        /// 正文中的关键协作调用包括 CloseConnection；具体条件和先后次序见方法体。
        /// </summary>
        private byte[]? CloseMalformedConnection()
        {
            CloseConnection();
            return null;
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 Disconnect。
        /// 正文中的关键协作调用包括 CloseConnection；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Disconnect()
        {
            lock (_sync)
                CloseConnection();
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Disconnect；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Disconnect();

        /// <summary>
        /// 执行关闭步骤；对应入口为 CloseConnection。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private void CloseConnection()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }
    }
}

