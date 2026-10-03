// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：McProtocolException。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
namespace Communication.Runtime.Protocols.Mitsubishi;

/// <summary>表示 MC 帧格式错误或 PLC 返回非零结束码。</summary>
public sealed class McProtocolException : IOException
{
    /// <summary>使用错误消息创建协议异常。</summary>
    public McProtocolException(string message) : base(message)
    {
    }

    /// <summary>PLC 返回的结束码；本地帧格式错误时为空。</summary>
    public ushort? EndCode { get; init; }
}
