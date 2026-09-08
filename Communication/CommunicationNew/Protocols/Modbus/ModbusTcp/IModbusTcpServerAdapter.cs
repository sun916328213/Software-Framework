namespace CommunicationNew.Protocols.Modbus.ModbusTcp;

/// <summary>
/// Modbus TCP 从站 SDK 的内部 seam；隐藏监听启动和数据区布局等 FluentModbus 细节。
/// </summary>
internal interface IModbusTcpServerAdapter : IDisposable
{
    /// <summary>从站是否正在处理 TCP 请求。</summary>
    bool IsRunning { get; }

    /// <summary>启动从站监听。</summary>
    void Start();

    /// <summary>停止从站并释放监听资源。</summary>
    void Stop();

    /// <summary>按兼容地址格式读取从站数据区。</summary>
    byte[] Read(string address, int length);

    /// <summary>按兼容地址格式写入从站数据区。</summary>
    void Write(string address, byte[] data);
}
