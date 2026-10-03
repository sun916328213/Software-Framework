// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：SerialByteStreamTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Pipelines。导入不代表所有类型都会在运行时加载。
using System.IO.Pipelines;

namespace Communication.Runtime.Transports;

/// <summary>
/// 基于 SerialPort BaseStream 的连续字节流 transport adapter。
/// 该类型只管理端口与 Pipe，不解释分隔符或协议帧。
/// </summary>
public sealed class SerialByteStreamTransport : IByteStreamTransport
{
    private readonly ISerialPortConnection _connection;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly AsyncDisposeOnce _disposeOnce;
    private PipeReader? _input;
    private PipeWriter? _output;
    private bool _disposed;

    /// <summary>使用不可变串口配置创建 transport。</summary>
    public SerialByteStreamTransport(SerialTransportOptions options)
        : this(options, new SystemSerialPortConnection(options))
    {
    }

    /// <summary>
    /// 初始化 SerialByteStreamTransport 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 ThrowIfNull、ValidateOptions；具体条件和先后次序见方法体。
    /// </summary>
    internal SerialByteStreamTransport(
        SerialTransportOptions options,
        ISerialPortConnection connection)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connection);
        ValidateOptions(options);
        _connection = connection;
        _disposeOnce = new AsyncDisposeOnce(DisposeCoreAsync);
    }

    /// <inheritdoc />
    public PipeReader Input =>
        _input ?? throw new InvalidOperationException("串口 transport 尚未打开。");

    /// <inheritdoc />
    public PipeWriter Output =>
        _output ?? throw new InvalidOperationException("串口 transport 尚未打开。");

    /// <inheritdoc />
    public bool IsOpen => _connection.IsOpen && _input is not null && _output is not null;

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 OpenAsync。
    /// 正文中的关键协作调用包括 ThrowIfDisposing、ConfigureAwait、WaitAsync、ThrowIfCancellationRequested、Open；具体条件和先后次序见方法体。
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

            cancellationToken.ThrowIfCancellationRequested();
            // SerialPort.Open 没有异步重载；打开后所有读写均通过可取消的 BaseStream。
            _connection.Open();
            cancellationToken.ThrowIfCancellationRequested();
            _input = PipeReader.Create(
                _connection.Stream,
                new StreamPipeReaderOptions(leaveOpen: true));
            _output = PipeWriter.Create(
                _connection.Stream,
                new StreamPipeWriterOptions(leaveOpen: true));
        }
        catch
        {
            if (_connection.IsOpen)
            {
                _connection.Close();
            }
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行关闭步骤；对应入口为 CloseAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、CompleteAsync、Close、Release；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsOpen && _input is null && _output is null)
            {
                return;
            }

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
            if (_connection.IsOpen)
            {
                _connection.Close();
            }
            _input = null;
            _output = null;
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
        _connection.Dispose();
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
    /// 处理当前类型提供的操作；对应入口为 ValidateOptions。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    private static void ValidateOptions(SerialTransportOptions options)
    {
        if (options.DataBits is < 5 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "串口数据位必须在 5 到 8 之间。");
        }
        if (options.BufferSize <= 0 || options.TimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "缓冲区和超时时间必须大于零。");
        }
    }
}
