namespace CommunicationNew.Protocols.Modbus.ModbusRtu;

/// <summary>
/// Modbus RTU 从站 SDK 的内部 seam；隐藏串口启动和数据区布局等 FluentModbus 细节。
/// </summary>
internal interface IModbusRtuServerAdapter : IDisposable
{
    /// <summary>从站是否正在处理串口请求。</summary>
    bool IsRunning { get; }

    /// <summary>启动从站。</summary>
    void Start();

    /// <summary>停止从站并释放串口等待。</summary>
    void Stop();

    /// <summary>按兼容地址格式读取从站数据区。</summary>
    byte[] Read(string address, int length);

    /// <summary>按兼容地址格式写入从站数据区。</summary>
    void Write(string address, byte[] data);
}
