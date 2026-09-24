using System.Net;
using System.Net.Sockets;

namespace CommunicationNew.Transports;

/// <summary>基于 <see cref="UdpClient"/> 的 UDP 数据报传输 adapter。</summary>
public sealed class UdpDatagramTransport : IDatagramTransport
{
    private readonly IPEndPoint _configuredLocalEndpoint;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly AsyncDisposeOnce _disposeOnce;
    private UdpClient? _client;
    private bool _disposed;

    /// <summary>创建绑定到指定本地端点的 UDP 传输。</summary>
    public UdpDatagramTransport(IPEndPoint localEndpoint)
    {
        ArgumentNullException.ThrowIfNull(localEndpoint);
        _configuredLocalEndpoint = new IPEndPoint(localEndpoint.Address, localEndpoint.Port);
        _disposeOnce = new AsyncDisposeOnce(DisposeCoreAsync);
    }

    /// <inheritdoc />
    public bool IsOpen => _client is not null;

    /// <inheritdoc />
    public IPEndPoint? LocalEndpoint => _client?.Client.LocalEndPoint as IPEndPoint;

    /// <inheritdoc />
    public async ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 释放可能在等待生命周期锁期间开始，获得锁后必须再次确认。
            ThrowIfDisposing();
            if (_client is not null)
            {
                return;
            }

            _client = new UdpClient(_configuredLocalEndpoint)
            {
                EnableBroadcast = true,
            };
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = _client;
            _client = null;
            // Dispose 会解除正在等待的 ReceiveAsync，不保留隐藏接收任务。
            client?.Dispose();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<Datagram> ReceiveAsync(
        CancellationToken cancellationToken = default)
    {
        var client = GetOpenClient();
        var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        return new Datagram(result.Buffer, result.RemoteEndPoint);
    }

    /// <inheritdoc />
    public async ValueTask SendAsync(
        Datagram datagram,
        CancellationToken cancellationToken = default)
    {
        var client = GetOpenClient();
        await client.SendAsync(
            datagram.Payload,
            datagram.RemoteEndpoint,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _disposeOnce.RunAsync();

    private async Task DisposeCoreAsync()
    {
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        _lifecycleGate.Dispose();
    }

    private void ThrowIfDisposing() =>
        ObjectDisposedException.ThrowIf(_disposed || _disposeOnce.IsStarted, this);

    private UdpClient GetOpenClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _client ?? throw new InvalidOperationException("UDP 传输尚未打开。");
    }
}
