namespace CommunicationNew.Abstractions;

/// <summary>
/// 可报告服务端对端上下线的可选能力。
/// 对端变化与 <see cref="IConnection"/> 表示的监听器生命周期彼此独立。
/// </summary>
public interface IPeerConnectionSource
{
    /// <summary>对端连接或断开时触发。</summary>
    event EventHandler<PeerConnectionChangedEventArgs>? PeerConnectionChanged;
}
