// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IPeerConnectionInfo。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>
/// P11 Core seam：服务端对端数量的可观测能力。
/// Controls 用此接口判断服务端广播是否可达，避免引用具体 Runtime 服务端类型。
/// </summary>
public interface IPeerConnectionInfo
{
    /// <summary>当前仍在线的对端数。</summary>
    int ActivePeerCount { get; }
}
