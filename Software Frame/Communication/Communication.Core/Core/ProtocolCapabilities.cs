// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ProtocolCapabilities。成员说明可结合下方具体实现阅读。
namespace Communication.Core;

/// <summary>
/// P11 Core seam：协议能力描述（无 WPF、无 Runtime 实现）。
/// UI 的显示名/图标留在 Controls；此处只保留分层判定所需的布尔能力，供 Controls 与测试共用。
/// </summary>
public static class ProtocolCapabilities
{
    /// <summary>协议是否支持服务端角色。</summary>
    public static bool SupportsServer(ProtocolType protocol) => protocol switch
    {
        ProtocolType.ModbusTcp => true,
        ProtocolType.ModbusRtu => true,
        ProtocolType.MitsubishiMc => false,
        ProtocolType.OpcUa => true,
        ProtocolType.Tcp => true,
        ProtocolType.Udp => true,
        ProtocolType.Serial => false,
        ProtocolType.Http => true,
        ProtocolType.WebApi => true,
        _ => false,
    };

    /// <summary>协议是否具有真实地址读写语义（与 IAddressReader 双重判定配合使用）。</summary>
    public static bool SupportsAddressRead(ProtocolType protocol) => protocol switch
    {
        ProtocolType.ModbusTcp => true,
        ProtocolType.ModbusRtu => true,
        ProtocolType.MitsubishiMc => true,
        ProtocolType.OpcUa => true,
        ProtocolType.Tcp => false,
        ProtocolType.Udp => false,
        ProtocolType.Serial => false,
        ProtocolType.Http => true,
        ProtocolType.WebApi => true,
        _ => false,
    };

    /// <summary>协议是否支持原始字节收发（仅字节流/数据报）。</summary>
    public static bool SupportsRaw(ProtocolType protocol) => protocol switch
    {
        ProtocolType.Tcp => true,
        ProtocolType.Udp => true,
        ProtocolType.Serial => true,
        _ => false,
    };
}
