namespace Secs.Variable;

/// <summary>SECS/HSMS 连接参数；文件读写由外层 SecsParam 负责。</summary>
public class CSecsParam
{
    public bool IsActiveMode { get; set; } = true;
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public int DeviceId { get; set; }
    public int BufferSize { get; set; } = 65535;
}
