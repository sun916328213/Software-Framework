using System.Net;

namespace CommunicationNew.Transports;

/// <summary>保留单个 UDP 消息边界以及远端端点的数据报。</summary>
public readonly record struct Datagram
{
    /// <summary>创建一个待发送或已接收的数据报。</summary>
    /// <param name="payload">完整数据报负载。</param>
    /// <param name="remoteEndpoint">发送目标或接收来源。</param>
    public Datagram(ReadOnlyMemory<byte> payload, IPEndPoint remoteEndpoint)
    {
        ArgumentNullException.ThrowIfNull(remoteEndpoint);
        Payload = payload;
        RemoteEndpoint = remoteEndpoint;
    }

    /// <summary>完整数据报负载。</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>发送目标或接收来源。</summary>
    public IPEndPoint RemoteEndpoint { get; }
}
