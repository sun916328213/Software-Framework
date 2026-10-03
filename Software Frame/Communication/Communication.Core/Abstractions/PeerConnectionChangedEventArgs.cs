// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：PeerConnectionChangedEventArgs。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>
/// 服务端对端连接数变化事件参数。
/// 该事件只描述某个对端的上下线，不代表服务端监听器的连接生命周期。
/// </summary>
public sealed class PeerConnectionChangedEventArgs : global::System.EventArgs
{
    /// <summary>
    /// 初始化 PeerConnectionChangedEventArgs 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PeerConnectionChangedEventArgs(bool isConnected, string? remoteAddress, int activePeerCount)
    {
        IsConnected = isConnected;
        RemoteAddress = remoteAddress;
        ActivePeerCount = activePeerCount;
    }

    /// <summary>对端是否上线。</summary>
    public bool IsConnected { get; }

    /// <summary>对端网络地址；底层无法取得时为 <see langword="null"/>。</summary>
    public string? RemoteAddress { get; }

    /// <summary>本次变化后仍在线的对端数。</summary>
    public int ActivePeerCount { get; }

    /// <summary>变化时间。</summary>
    public DateTime Timestamp { get; } = DateTime.Now;
}
