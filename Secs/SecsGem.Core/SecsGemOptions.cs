namespace SecsGem.Core;

/// <summary>SECS/HSMS 连接参数；文件读写由外层 SecsParam 负责。</summary>
public class SecsGemOptions
{
    public bool IsActiveMode { get; set; } = true;
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public int DeviceId { get; set; }
    public int BufferSize { get; set; } = 65535;

    /// <summary>Validate the HSMS endpoint before starting a connection.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(IpAddress))
            throw new ArgumentException("HSMS address is required.", nameof(IpAddress));
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port), "TCP port must be between 1 and 65535.");
        if (DeviceId is < 0 or > 32767)
            throw new ArgumentOutOfRangeException(nameof(DeviceId), "HSMS device ID must fit in 15 bits.");
        if (BufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(BufferSize), "Buffer size must be positive.");
    }
}
