// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：UdpDatagramTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、System.Net.Sockets。导入不代表所有类型都会在运行时加载。
using System.Net;
using System.Net.Sockets;

namespace Communication.Runtime.Transports;

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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 OpenAsync。
    /// 正文中的关键协作调用包括 ThrowIfDisposing、ConfigureAwait、WaitAsync、Release；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
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
    /// <remarks>
    /// 执行关闭步骤；对应入口为 CloseAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、Release；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
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
    /// <remarks>
    /// 处理接收操作；对应入口为 ReceiveAsync。
    /// 正文中的关键协作调用包括 GetOpenClient、ConfigureAwait、ReceiveAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask<Datagram> ReceiveAsync(
        CancellationToken cancellationToken = default)
    {
        var client = GetOpenClient();
        var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        return new Datagram(result.Buffer, result.RemoteEndPoint);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行发送操作；对应入口为 SendAsync。
    /// 正文中的关键协作调用包括 GetOpenClient、ConfigureAwait、SendAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
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
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public ValueTask DisposeAsync() => _disposeOnce.RunAsync();

    /// <summary>
    /// 执行资源释放步骤；对应入口为 DisposeCoreAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、CloseAsync、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    private async Task DisposeCoreAsync()
    {
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        _lifecycleGate.Dispose();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ThrowIfDisposing。
    /// 正文中的关键协作调用包括 ThrowIf；具体条件和先后次序见方法体。
    /// </summary>
    private void ThrowIfDisposing() =>
        ObjectDisposedException.ThrowIf(_disposed || _disposeOnce.IsStarted, this);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetOpenClient。
    /// 正文中的关键协作调用包括 ThrowIf；具体条件和先后次序见方法体。
    /// </summary>
    private UdpClient GetOpenClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _client ?? throw new InvalidOperationException("UDP 传输尚未打开。");
    }
}
