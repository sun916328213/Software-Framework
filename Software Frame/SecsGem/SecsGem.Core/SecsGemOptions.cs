// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemOptions。成员说明可结合下方具体实现阅读。
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Validate。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、nameof；具体条件和先后次序见方法体。
    /// </remarks>
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
