using System.ComponentModel;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 新通讯节点的数据收发格式。
/// </summary>
public enum ENetDataFormat
{
    /// <summary>UTF8 文本：直接按 UTF8 编解码字符串。</summary>
    [Description("UTF8")]
    UTF8 = 0,

    /// <summary>HEX：如 "01 03 00 00 00 02"，解析时容忍空格/横杠/0x 前缀。</summary>
    [Description("HEX")]
    Hex = 1,
}
