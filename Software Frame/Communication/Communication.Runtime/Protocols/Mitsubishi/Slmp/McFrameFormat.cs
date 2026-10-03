// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：McFrameFormat。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
namespace Communication.Runtime.Protocols.Mitsubishi;

/// <summary>SLMP/MC 二进制帧格式。</summary>
public enum McFrameFormat
{
    /// <summary>不携带序列号的 QnA 兼容 3E 帧。</summary>
    ThreeE,

    /// <summary>携带序列号、可关联请求响应的 4E 帧。</summary>
    FourE,
}
