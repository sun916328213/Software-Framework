using System.Net.Sockets;

namespace CommunicationNew.Protocols.Socket;

/// <summary>TCP 服务端监听器的内部边界，用于明确监听句柄与 accept 循环的所有权。</summary>
internal interface ITcpServerListener : IDisposable
{
    /// <summary>监听器是否处于启动状态。</summary>
    bool IsListening { get; }

    /// <summary>实际监听的端口号（Port=0 时由系统分配，Start 后可读回）。</summary>
    int LocalPort { get; }

    /// <summary>按指定连接积压上限启动监听。</summary>
    void Start(int backlog);

    /// <summary>停止监听并解除正在等待的 accept。</summary>
    void Stop();

    /// <summary>异步接受一个 TCP 客户端。</summary>
    ValueTask<TcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken);
}
