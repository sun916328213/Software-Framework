using System.IO.Pipelines;

namespace CommunicationNew.Transports;

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
    public ValueTask DisposeAsync() => _disposeOnce.RunAsync();

    private async Task DisposeCoreAsync()
    {
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        _connection.Dispose();
        _disposed = true;
        _lifecycleGate.Dispose();
    }

    private void ThrowIfDisposing() =>
        ObjectDisposedException.ThrowIf(_disposed || _disposeOnce.IsStarted, this);

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
