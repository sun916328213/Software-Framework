namespace CommunicationNew.Transports;

/// <summary>隔离平台 SerialPort 的内部测试 seam，不暴露给库调用方。</summary>
internal interface ISerialPortConnection : IDisposable
{
    bool IsOpen { get; }
    Stream Stream { get; }
    void Open();
    void Close();
}
