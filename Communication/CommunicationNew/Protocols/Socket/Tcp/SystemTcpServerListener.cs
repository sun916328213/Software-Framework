using System.Net;
using System.Net.Sockets;

namespace CommunicationNew.Protocols.Socket;

/// <summary>把系统 <see cref="TcpListener"/> 适配为项目内部监听器边界。</summary>
internal sealed class SystemTcpServerListener : ITcpServerListener
{
    private readonly TcpListener _listener;

    /// <summary>创建绑定到指定地址和端口的监听器。</summary>
    public SystemTcpServerListener(IPAddress localAddress, int port)
    {
        ArgumentNullException.ThrowIfNull(localAddress);
        _listener = new TcpListener(localAddress, port);
    }

    /// <inheritdoc />
    public bool IsListening { get; private set; }

    /// <inheritdoc />
    public void Start(int backlog)
    {
        _listener.Start(backlog);
        IsListening = true;
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!IsListening)
        {
            return;
        }

        IsListening = false;
        _listener.Stop();
    }

    /// <inheritdoc />
    public ValueTask<TcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken) =>
        _listener.AcceptTcpClientAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => Stop();
}
