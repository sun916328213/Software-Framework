namespace CommunicationNew.Abstractions;

/// <summary>
/// P11 Core seam：服务端对端数量的可观测能力。
/// Controls 用此接口判断服务端广播是否可达，避免引用具体 Runtime 服务端类型。
/// </summary>
public interface IPeerConnectionInfo
{
    /// <summary>当前仍在线的对端数。</summary>
    int ActivePeerCount { get; }
}
