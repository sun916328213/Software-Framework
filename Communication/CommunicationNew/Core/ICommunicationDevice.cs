using CommunicationNew.Core.EventArgs;

namespace CommunicationNew.Core
{
    /// <summary>
    /// 统一通信设备接口 —— 所有协议（Socket/Modbus/OPCUA/HTTP/串口/MC等）实现此接口。
    /// 外部调用方只需持有此接口引用，切换协议时仅需更换 Param 配置，无需修改业务代码。
    /// </summary>
    public interface ICommunicationDevice : IDisposable, IAsyncDisposable
    {
        // ===== 识别与状态 =====

        /// <summary>设备名称</summary>
        string DeviceName { get; set; }

        /// <summary>设备唯一ID</summary>
        string DeviceId { get; }

        /// <summary>协议类型</summary>
        ProtocolType ProtocolType { get; }

        /// <summary>角色（客户端/服务器）</summary>
        DeviceRole Role { get; }

        /// <summary>是否已连接</summary>
        bool IsConnected { get; }

        /// <summary>单次读写超时（毫秒）</summary>
        int TimeoutMs { get; set; }

        // ===== 事件 =====

        /// <summary>连接状态变化</summary>
        event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>数据到达（被动接收模式）</summary>
        event EventHandler<DataReceivedEventArgs>? DataReceived;

        /// <summary>通信异常</summary>
        event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred;

        // ===== 生命周期 =====

        /// <summary>建立连接</summary>
        Task<bool> ConnectAsync(CancellationToken ct = default);

        /// <summary>断开连接</summary>
        Task DisconnectAsync(CancellationToken ct = default);

        // ===== 统一读写 =====

        /// <summary>
        /// 按地址读取数据。
        /// 地址格式因协议而异：
        ///   Modbus: "HR1" / "40001" / "D100"
        ///   OpcUa:  "ns=2;s=Temperature"
        ///   HTTP:   "/api/sensor/read"
        ///   MC:     "D100" / "M10"
        /// </summary>
        /// <param name="address">协议相关地址</param>
        /// <param name="length">读取长度（字节数或寄存器数）</param>
        Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default);

        /// <summary>
        /// 按地址写入数据。
        /// </summary>
        /// <param name="address">协议相关地址</param>
        /// <param name="data">待写入的字节数据</param>
        Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default);

        // ===== 流式收发（适用于 Socket / Serial / MC 等流协议）=====

        /// <summary>仅发送原始数据，不等待回复</summary>
        Task<bool> SendAsync(byte[] data, CancellationToken ct = default);

        /// <summary>发送数据并等待回复</summary>
        Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default);
    }
}
