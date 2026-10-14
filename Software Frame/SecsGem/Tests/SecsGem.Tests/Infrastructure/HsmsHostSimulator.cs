// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HsmsHostSimulator、HsmsMessage。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Tests、System、System.IO、System.Net、System.Net.Sockets、System.Threading。导入不代表所有类型都会在运行时加载。
using SecsGem.Tests;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SecsGem.Tests.Infrastructure
{
    /// <summary>
    /// 最小 HSMS Host 模拟器（仅测试用）：
    /// 监听 TCP 端口等待设备(ACTIVE)连入，完成 Select 握手，
    /// 收发数据消息（4字节长度 + 10字节头 + Item 体）。
    /// </summary>
    public sealed class HsmsHostSimulator : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task<TcpClient> _acceptTask;
        private TcpClient _client;
        private NetworkStream _stream;

        // HSMS Session Type 常量 —— 与 SEMI E37 一致（Select.req=0x01/rsp=0x02），
        // 和 secs4net-base 的 MessageType 枚举相同。不要改成别的"标准值"：
        // 之前这里有条注释声称标准是 5/6，那是错的，按它改会让握手永远完不成。
        public const byte StypeData = 0x00;
        public const byte StypeSelectReq = 0x01;
        public const byte StypeSelectRsp = 0x02;

        /// <summary>
        /// HsmsMessage 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public readonly struct HsmsMessage
        {
            public byte DeviceIdHigh { get; init; }
            public byte DeviceIdLow { get; init; }
            public bool WBit { get; init; }
            public byte S { get; init; }
            public byte F { get; init; }
            public byte PType { get; init; }
            public byte SType { get; init; }
            public uint SystemBytes { get; init; }
            public byte[] Body { get; init; }
        }

        /// <summary>
        /// 初始化 HsmsHostSimulator 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 AcceptTcpClientAsync；具体条件和先后次序见方法体。
        /// </summary>
        private HsmsHostSimulator(TcpListener listener)
        {
            _listener = listener;
            _acceptTask = _listener.AcceptTcpClientAsync();
        }

        /// <summary>
        /// 测试相关入口 Start；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Start；具体条件和先后次序见方法体。
        /// </summary>
        public static HsmsHostSimulator Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new HsmsHostSimulator(listener);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        /// <summary>等待设备(主动模式)接入</summary>
        public async Task WaitForDeviceAsync(CancellationToken ct)
        {
            var completed = await Task.WhenAny(_acceptTask, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);
            if (completed != _acceptTask) throw new TimeoutException("设备未在时限内连入");
            _client = await _acceptTask.ConfigureAwait(false);
            _stream = _client.GetStream();
        }

        /// <summary>读取设备的 Select 请求并回 Select 响应，完成链路选择</summary>
        public async Task CompleteSelectAsync(CancellationToken ct)
        {
            var req = await ReadMessageAsync(ct).ConfigureAwait(false);
            if (req.SType != StypeSelectReq)
            {
                throw new InvalidOperationException($"期望 Select.req，收到 SType=0x{req.SType:X2}");
            }

            SendHeaderOnly(req.SystemBytes, StypeSelectRsp, ct);
        }

        /// <summary>发送纯头消息（Select.rsp 等）</summary>
        public void SendHeaderOnly(uint systemBytes, byte stype, CancellationToken ct)
        {
            var header = new byte[10];
            WriteHeader(header, 0xFFFF, false, 0, 0, stype, systemBytes);
            SendFrame(header, Array.Empty<byte>(), ct);
        }

        /// <summary>发送数据消息（Primary，W 位可选）</summary>
        public void SendDataMessage(byte s, byte f, bool w, ushort deviceId, uint systemBytes, byte[] itemBytes, CancellationToken ct)
        {
            var header = new byte[10];
            WriteHeader(header, deviceId, w, s, f, StypeData, systemBytes);
            SendFrame(header, itemBytes ?? Array.Empty<byte>(), ct);
        }

        /// <summary>读取一条完整 HSMS 消息</summary>
        public async Task<HsmsMessage> ReadMessageAsync(CancellationToken ct)
        {
            byte[] lengthBytes = await ReadExactAsync(_stream, 4, ct).ConfigureAwait(false);
            int length = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) | (lengthBytes[2] << 8) | lengthBytes[3];
            if (length < 10 || length > 10_000_000)
            {
                throw new InvalidDataException($"非法消息长度: {length}");
            }

            byte[] payload = await ReadExactAsync(_stream, length, ct).ConfigureAwait(false);

            byte[] header = new byte[10];
            Array.Copy(payload, header, 10);
            byte[] body = new byte[length - 10];
            Array.Copy(payload, 10, body, 0, body.Length);

            return new HsmsMessage
            {
                DeviceIdHigh = header[0],
                DeviceIdLow = header[1],
                WBit = (header[2] & 0x80) != 0,
                S = (byte)(header[2] & 0x7F),
                F = header[3],
                PType = header[4],
                SType = header[5],
                SystemBytes = ((uint)header[6] << 24) | ((uint)header[7] << 16) | ((uint)header[8] << 8) | header[9],
                Body = body,
            };
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            _stream?.Dispose();
            _client?.Dispose();
            _listener.Stop();
        }

        // ── 私有辅助 ──

        private static void WriteHeader(byte[] header, ushort deviceId, bool w, byte s, byte f, byte stype, uint systemBytes)
        {
            header[0] = (byte)(deviceId >> 8);
            header[1] = (byte)(deviceId & 0xFF);
            header[2] = (byte)((w ? 0x80 : 0x00) | (s & 0x7F));
            header[3] = f;
            header[4] = 0x00; // PType
            header[5] = stype;
            header[6] = (byte)(systemBytes >> 24);
            header[7] = (byte)(systemBytes >> 16);
            header[8] = (byte)(systemBytes >> 8);
            header[9] = (byte)(systemBytes & 0xFF);
        }

        /// <summary>
        /// 测试相关入口 SendFrame；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、Copy、Register、Write、Flush；具体条件和先后次序见方法体。
        /// </summary>
        private void SendFrame(byte[] header, byte[] body, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            int length = header.Length + body.Length;
            byte[] frame = new byte[4 + length];
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)(length & 0xFF);
            Array.Copy(header, 0, frame, 4, header.Length);
            Array.Copy(body, 0, frame, 4 + header.Length, body.Length);

            using var __ = ct.Register(() => _stream?.Dispose());
            _stream.Write(frame, 0, frame.Length);
            _stream.Flush();
        }

        /// <summary>
        /// 测试相关入口 ReadExactAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Register、Dispose、ConfigureAwait、ReadAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken ct)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            using var __ = ct.Register(() => stream.Dispose());
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset, ct).ConfigureAwait(false);
                if (read == 0) throw new IOException("连接已关闭");
                offset += read;
            }
            return buffer;
        }
    }
}
