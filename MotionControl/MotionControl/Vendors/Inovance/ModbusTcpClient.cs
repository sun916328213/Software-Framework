using System.Net.Sockets;

namespace MotionControl.Vendors.Inovance
{
    /// <summary>
    /// 轻量Modbus TCP主站客户端（标准MBAP协议，FC03/06/10）
    /// <para>用于与汇川H3U/H5U/Easy/AM系列控制器网口通讯，避免引入额外依赖</para>
    /// </summary>
    internal sealed class ModbusTcpClient : IDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private ushort _txId;

        /// <summary>连接超时时间（毫秒）</summary>
        public int TimeoutMs { get; set; } = 2000;

        /// <summary>
        /// 建立TCP连接
        /// </summary>
        public bool Connect(string ip, int port)
        {
            try
            {
                Disconnect();
                _client = new TcpClient();
                var task = _client.ConnectAsync(ip, port);
                if (!task.Wait(TimeoutMs) || !_client.Connected)
                {
                    Dispose();
                    return false;
                }
                _client.ReceiveTimeout = TimeoutMs;
                _client.SendTimeout = TimeoutMs;
                _stream = _client.GetStream();
                return true;
            }
            catch
            {
                Dispose();
                return false;
            }
        }

        /// <summary>是否已连接</summary>
        public bool IsConnected => _client?.Connected == true;

        /// <summary>
        /// 读保持寄存器（FC03）
        /// </summary>
        /// <param name="slaveId">从站号</param>
        /// <param name="address">寄存器地址（0基）</param>
        /// <param name="count">寄存器数量</param>
        /// <returns>寄存器值数组，失败返回null</returns>
        public short[]? ReadHoldingRegisters(byte slaveId, ushort address, ushort count)
        {
            try
            {
                var request = BuildRequest(slaveId, 0x03);
                // FC03：起始地址(2) + 数量(2)
                request.AddRange(new byte[] { (byte)(address >> 8), (byte)address, (byte)(count >> 8), (byte)count });

                var response = Transact(request.ToArray(), 9 + count * 2);
                if (response == null || response[7] != 0x03)
                    return null;
                var result = new short[count];
                for (int i = 0; i < count; i++)
                    result[i] = (short)((response[9 + i * 2] << 8) | response[10 + i * 2]);
                return result;
            }
            catch
            {
                Dispose();
                return null;
            }
        }

        /// <summary>
        /// 写单个寄存器（FC06）
        /// </summary>
        public bool WriteSingleRegister(byte slaveId, ushort address, short value)
        {
            try
            {
                var request = BuildRequest(slaveId, 0x06);
                request.AddRange(new byte[] { (byte)(address >> 8), (byte)address, (byte)(value >> 8), (byte)value });
                var response = Transact(request.ToArray(), 12);
                return response != null && response[7] == 0x06;
            }
            catch
            {
                Dispose();
                return false;
            }
        }

        /// <summary>
        /// 写多个寄存器（FC10）
        /// </summary>
        public bool WriteMultipleRegisters(byte slaveId, ushort address, short[] values)
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
                var response = Transact(request.ToArray(), 12);
                return response != null && response[7] == 0x10;
            }
            catch
            {
                Dispose();
                return false;
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

        /// <summary>发送请求并接收响应（自动修正长度域）</summary>
        private byte[]? Transact(byte[] request, int expectedLength)
        {
            if (_stream == null || !_stream.CanWrite)
                return null;
            // 修正长度域 = UnitId + Func + 数据字节数
            int len = request.Length - 6;
            request[4] = (byte)(len >> 8);
            request[5] = (byte)len;

            _stream.Write(request, 0, request.Length);

            var buffer = new byte[expectedLength];
            int received = 0;
            while (received < expectedLength)
            {
                int n = _stream.Read(buffer, received, expectedLength - received);
                if (n <= 0)
                    return null;
                received += n;
            }
            return buffer;
        }

        public void Disconnect() => Dispose();

        public void Dispose()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }
    }
}
