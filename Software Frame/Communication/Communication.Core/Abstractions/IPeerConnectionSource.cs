// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IPeerConnectionSource。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>
/// 可报告服务端对端上下线的可选能力。
/// 对端变化与 <see cref="IConnection"/> 表示的监听器生命周期彼此独立。
/// </summary>
public interface IPeerConnectionSource
{
    /// <summary>对端连接或断开时触发。</summary>
    event EventHandler<PeerConnectionChangedEventArgs>? PeerConnectionChanged;
}
