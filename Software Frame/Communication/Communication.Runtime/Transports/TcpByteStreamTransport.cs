// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：TcpByteStreamTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Pipelines、System.Net.Sockets。导入不代表所有类型都会在运行时加载。
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Communication.Runtime.Transports;

/// <summary>
/// 基于 <see cref="TcpClient"/> 的连续字节流传输适配器。
/// 该类型只负责建立连接和搬运字节，不解析协议帧，也不执行重试或重连。
/// </summary>
public sealed class TcpByteStreamTransport : IByteStreamTransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly AsyncDisposeOnce _disposeOnce;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private PipeReader? _input;
    private PipeWriter? _output;
    private bool _disposed;

    /// <summary>创建一个 TCP 字节流传输。</summary>
    /// <param name="host">远端主机名或 IP 地址。</param>
    /// <param name="port">远端 TCP 端口。</param>
    public TcpByteStreamTransport(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (port is <= 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _host = host;
        _port = port;
        _disposeOnce = new AsyncDisposeOnce(DisposeCoreAsync);
    }

    /// <inheritdoc />
    public PipeReader Input =>
        _input ?? throw new InvalidOperationException("TCP 传输尚未打开。");

    /// <inheritdoc />
    public PipeWriter Output =>
        _output ?? throw new InvalidOperationException("TCP 传输尚未打开。");

    /// <inheritdoc />
    public bool IsOpen => _client?.Connected == true && _stream is not null;

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 OpenAsync。
    /// 正文中的关键协作调用包括 ThrowIfDisposing、ConfigureAwait、WaitAsync、ConnectAsync、GetStream；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 释放可能在等待生命周期锁期间开始，获得锁后必须再次确认。
            ThrowIfDisposing();
            if (IsOpen)
            {
                return;
            }

            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);
                var stream = client.GetStream();

                _client = client;
                _stream = stream;
                _input = PipeReader.Create(
                    stream,
                    new StreamPipeReaderOptions(leaveOpen: true));
                _output = PipeWriter.Create(
                    stream,
                    new StreamPipeWriterOptions(leaveOpen: true));
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行关闭步骤；对应入口为 CloseAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、CompleteAsync、Release；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is null && _stream is null)
            {
                return;
            }

            // 先取消挂起的 Pipe 操作，再完成 reader/writer，保证等待中的 IO 可以退出。
            _input?.CancelPendingRead();
            _output?.CancelPendingFlush();

            if (_output is not null)
            {
                await _output.CompleteAsync().ConfigureAwait(false);
            }

            if (_input is not null)
            {
                await _input.CompleteAsync().ConfigureAwait(false);
            }

            _stream?.Dispose();
            _client?.Dispose();
            _input = null;
            _output = null;
            _stream = null;
            _client = null;
        }
        finally
        {
            _lifecycleGate.Release();
        }
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
}
