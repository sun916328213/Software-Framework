using CommunicationNew.Abstractions;
using CommunicationNew.Transports;
using System.Runtime.ExceptionServices;

namespace CommunicationNew.Runtime;

/// <summary>
/// 单条请求/响应连接的唯一所有者。
/// 负责串行化生命周期、关联请求响应、执行总超时并按顺序异步释放资源。
/// </summary>
public sealed class ConnectionSession<TRequest, TResponse> :
    IConnection,
    IRequestClient<TRequest, TResponse>
{
    private readonly IByteStreamTransport _transport;
    private readonly IRequestCodec<TRequest, TResponse> _codec;
    private readonly TimeSpan _operationTimeout;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private CancellationTokenSource? _generationCts;
    private int _correlationId;
    private bool _disposed;

    /// <summary>创建一个单请求在途的连接 session。</summary>
    /// <param name="transport">连续字节流传输适配器。</param>
    /// <param name="codec">无状态请求/响应 codec。</param>
    /// <param name="operationTimeout">包含排队和 IO 在内的单次操作总超时。</param>
    /// <param name="timeProvider">时间源；测试可注入虚拟时间。</param>
    public ConnectionSession(
        IByteStreamTransport transport,
        IRequestCodec<TRequest, TResponse> codec,
        TimeSpan operationTimeout,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(codec);
        if (operationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout));
        }

        _transport = transport;
        _codec = codec;
        _operationTimeout = operationTimeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    /// <inheritdoc />
    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == ConnectionState.Connected)
            {
                return;
            }

            State = ConnectionState.Connecting;
            try
            {
                await _transport.OpenAsync(cancellationToken).ConfigureAwait(false);
                _generationCts?.Dispose();
                _generationCts = new CancellationTokenSource();
                State = ConnectionState.Connected;
            }
            catch (OperationCanceledException)
            {
                State = ConnectionState.Disconnected;
                throw;
            }
            catch
            {
                State = ConnectionState.Faulted;
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var generationToken = _generationCts?.Token;
        if (State != ConnectionState.Connected || generationToken is null)
        {
            throw new InvalidOperationException("The connection is not established.");
        }

        using var timeoutCts = new CancellationTokenSource(_operationTimeout, _timeProvider);
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            generationToken.Value,
            timeoutCts.Token);
        var operationToken = operationCts.Token;

        var requestGateEntered = false;
        try
        {
            await _requestGate.WaitAsync(operationToken).ConfigureAwait(false);
            requestGateEntered = true;
            if (State != ConnectionState.Connected)
            {
                throw new InvalidOperationException("The connection is not established.");
            }

            var correlationId = unchecked((ushort)Interlocked.Increment(ref _correlationId));
            var encoded = _codec.Encode(request, correlationId);
            await _transport.Output.WriteAsync(encoded, operationToken).ConfigureAwait(false);

            while (true)
            {
                var readResult = await _transport.Input.ReadAsync(operationToken).ConfigureAwait(false);
                var buffer = readResult.Buffer;
                if (_codec.TryDecode(buffer, correlationId, out var response, out var consumed))
                {
                    _transport.Input.AdvanceTo(consumed);
                    return response;
                }

                _transport.Input.AdvanceTo(buffer.Start, buffer.End);
                if (readResult.IsCompleted)
                {
                    throw new EndOfStreamException("The connection closed before a complete response arrived.");
                }
            }
        }
        catch (OperationCanceledException ex) when (
            timeoutCts.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested &&
            !generationToken.Value.IsCancellationRequested)
        {
            // 总超时由 session 转换为 TimeoutException，调用者可与主动取消分别处理。
            throw new TimeoutException(
                $"通信操作在 {_operationTimeout} 内未完成。",
                ex);
        }
        finally
        {
            if (requestGateEntered)
            {
                _requestGate.Release();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is ConnectionState.Disconnected or ConnectionState.Disposed)
            {
                return;
            }

            State = ConnectionState.Stopping;
            _generationCts?.Cancel();
            try
            {
                await _transport.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // 即使传输层关闭失败，也必须结束当前连接代际，避免状态永久停在 Stopping。
                _generationCts?.Dispose();
                _generationCts = null;
                State = ConnectionState.Disconnected;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        Exception? disposalError = null;
        try
        {
            await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            disposalError = ex;
        }

        try
        {
            // generation 取消后等待在途请求离开临界区，才能安全释放请求锁。
            await _requestGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            _requestGate.Release();
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            disposalError = disposalError is null
                ? ex
                : new AggregateException(disposalError, ex);
        }
        finally
        {
            _disposed = true;
            State = ConnectionState.Disposed;
            _generationCts?.Dispose();
            _lifecycleGate.Dispose();
            _requestGate.Dispose();
        }

        if (disposalError is not null)
        {
            ExceptionDispatchInfo.Capture(disposalError).Throw();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
