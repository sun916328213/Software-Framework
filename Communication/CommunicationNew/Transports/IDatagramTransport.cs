using System.Net;

namespace CommunicationNew.Transports;

/// <summary>
/// 保留消息边界的数据报传输 seam。
/// 每次发送对应一个完整数据报；实现不得合并或拆分负载。
/// </summary>
public interface IDatagramTransport : IAsyncDisposable
{
    /// <summary>传输是否已经打开。</summary>
    bool IsOpen { get; }

    /// <summary>打开后实际绑定的本地端点。</summary>
    IPEndPoint? LocalEndpoint { get; }

    /// <summary>打开并绑定传输；重复打开保持幂等，释放开始后拒绝新的打开请求。</summary>
    /// <exception cref="ObjectDisposedException">传输已经开始释放。</exception>
    ValueTask OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>关闭传输并取消底层等待；重复关闭保持幂等。</summary>
    ValueTask CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>接收一个完整数据报。</summary>
    ValueTask<Datagram> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>发送一个完整数据报。</summary>
    ValueTask SendAsync(
        Datagram datagram,
        CancellationToken cancellationToken = default);
}
