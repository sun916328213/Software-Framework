// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ConnectionSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Abstractions、Communication.Runtime.Transports、System.Buffers、System.Diagnostics、System.IO.Pipelines、System.Runtime.ExceptionServices。导入不代表所有类型都会在运行时加载。
using Communication.Core.Abstractions;
using Communication.Runtime.Transports;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace Communication.Runtime;

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
    private readonly ILogger? _logger;
    private readonly CommunicationDiagnosticContext _diagnosticContext;
    private readonly ReconnectSupervisor? _reconnectSupervisor;
    private readonly int _maxQueuedRequests;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly object _disposeSync = new();
    private CancellationTokenSource? _generationCts;
    private Task? _disposeTask;
    private int _correlationId;
    private int _queuedRequestCount;
    private int _disposeStarted;
    private bool _connectionMetricActive;
    private volatile bool _desiredConnected;
    private bool _disposed;

    /// <summary>创建一个单请求在途的连接 session。</summary>
    /// <param name="transport">连续字节流传输适配器。</param>
    /// <param name="codec">无状态请求/响应 codec。</param>
    /// <param name="operationTimeout">包含排队和 IO 在内的单次操作总超时。</param>
    /// <param name="timeProvider">时间源；测试可注入虚拟时间。</param>
    /// <param name="logger">标准 .NET 日志入口；为空时不产生日志。</param>
    /// <param name="reconnectOptions">意外断开后的恢复策略；为空时不自动重连。</param>
    /// <param name="maxQueuedRequests">等待单请求执行权的最大请求数。</param>
    public ConnectionSession(
        IByteStreamTransport transport,
        IRequestCodec<TRequest, TResponse> codec,
        TimeSpan operationTimeout,
        TimeProvider? timeProvider = null,
        ILogger? logger = null,
        ConnectionReconnectOptions? reconnectOptions = null,
        int maxQueuedRequests = 256)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(codec);
        if (operationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout));
        }
        if (maxQueuedRequests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQueuedRequests));
        }

        _transport = transport;
        _codec = codec;
        _operationTimeout = operationTimeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
        _maxQueuedRequests = maxQueuedRequests;
        _diagnosticContext = new CommunicationDiagnosticContext(
            _codec.GetType().Name,
            _transport.GetType().Name);
        if (reconnectOptions is not null)
        {
            _reconnectSupervisor = new ReconnectSupervisor(
                reconnectOptions,
                _timeProvider,
                ReconnectOnceAsync,
                () => _desiredConnected,
                _diagnosticContext);
        }
    }

    /// <inheritdoc />
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfDisposed、ConfigureAwait、StopAsync、WaitAsync、CloseAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (State == ConnectionState.Faulted)
        {
            _desiredConnected = false;
            if (_reconnectSupervisor is not null)
            {
                await _reconnectSupervisor.StopAsync().ConfigureAwait(false);
            }
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 释放可能在等待生命周期锁期间开始，获得锁后必须再次确认。
            ThrowIfDisposed();
            if (State == ConnectionState.Connected)
            {
                _desiredConnected = true;
                return;
            }

            var resetTransport = State == ConnectionState.Faulted;
            State = ConnectionState.Connecting;
            try
            {
                if (resetTransport)
                {
                    // Faulted 可能仍保留底层句柄；先关闭才能建立全新的连接代际。
                    await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                }
                await _transport.OpenAsync(cancellationToken).ConfigureAwait(false);
                _generationCts?.Dispose();
                _generationCts = new CancellationTokenSource();
                State = ConnectionState.Connected;
                _desiredConnected = true;
                if (!_connectionMetricActive)
                {
                    CommunicationDiagnostics.RecordActiveConnection(1, _diagnosticContext);
                    _connectionMetricActive = true;
                }
                _logger?.LogInformation(
                    "Connection session state changed to {ConnectionState}.",
                    State);
                CommunicationDiagnostics.RecordConnectionAttempt("success", _diagnosticContext);
            }
            catch (OperationCanceledException)
            {
                _desiredConnected = false;
                State = ConnectionState.Disconnected;
                CommunicationDiagnostics.RecordConnectionAttempt("canceled", _diagnosticContext);
                throw;
            }
            catch (Exception ex)
            {
                _desiredConnected = false;
                State = ConnectionState.Faulted;
                CommunicationDiagnostics.RecordConnectionAttempt("faulted", _diagnosticContext);
                _logger?.LogError(
                    ex,
                    "Connection session failed while entering {ConnectionState}.",
                    State);
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
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfDisposed、CreateLinkedTokenSource、StartRequestActivity、GetTimestamp、StartPhaseActivity；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<TResponse> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var generation = _generationCts;
        if (State != ConnectionState.Connected || generation is null)
        {
            throw new InvalidOperationException("The connection is not established.");
        }
        var generationToken = generation.Token;

        using var timeoutCts = new CancellationTokenSource(_operationTimeout, _timeProvider);
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            generationToken,
            timeoutCts.Token);
        var operationToken = operationCts.Token;
        using var activity = CommunicationDiagnostics.StartRequestActivity(_diagnosticContext);
        var startedAt = _timeProvider.GetTimestamp();
        var outcome = "faulted";

        var requestGateEntered = false;
        try
        {
            using (CommunicationDiagnostics.StartPhaseActivity("queue", _diagnosticContext))
            {
                if (_requestGate.Wait(0))
                {
                    requestGateEntered = true;
                }
                else
                {
                    var queuedCount = Interlocked.Increment(ref _queuedRequestCount);
                    if (queuedCount > _maxQueuedRequests)
                    {
                        Interlocked.Decrement(ref _queuedRequestCount);
                        throw new RequestQueueFullException(_maxQueuedRequests);
                    }

                    CommunicationDiagnostics.RecordQueuedRequest(1, _diagnosticContext);
                    try
                    {
                        await _requestGate.WaitAsync(operationToken).ConfigureAwait(false);
                        requestGateEntered = true;
                    }
                    finally
                    {
                        // 获得执行权和排队期间取消都必须归还队列深度。
                        Interlocked.Decrement(ref _queuedRequestCount);
                        CommunicationDiagnostics.RecordQueuedRequest(-1, _diagnosticContext);
                    }
                }
            }
            if (State != ConnectionState.Connected)
            {
                throw new InvalidOperationException("The connection is not established.");
            }

            var correlationId = unchecked((ushort)Interlocked.Increment(ref _correlationId));
            ReadOnlyMemory<byte> encoded;
            using (CommunicationDiagnostics.StartPhaseActivity("encode", _diagnosticContext))
            {
                encoded = _codec.Encode(request, correlationId);
            }
            using (CommunicationDiagnostics.StartPhaseActivity("send", _diagnosticContext))
            {
                await _transport.Output.WriteAsync(encoded, operationToken).ConfigureAwait(false);
            }
            CommunicationDiagnostics.RecordSentBytes(encoded.Length, _diagnosticContext);

            while (true)
            {
                ReadResult readResult;
                using (CommunicationDiagnostics.StartPhaseActivity("wait", _diagnosticContext))
                {
                    readResult = await _transport.Input.ReadAsync(operationToken).ConfigureAwait(false);
                }
                var buffer = readResult.Buffer;
                TResponse response;
                SequencePosition consumed;
                bool decoded;
                using (CommunicationDiagnostics.StartPhaseActivity("decode", _diagnosticContext))
                {
                    decoded = _codec.TryDecode(
                        buffer,
                        correlationId,
                        out response,
                        out consumed);
                }
                if (decoded)
                {
                    var consumedLength = buffer.Slice(0, consumed).Length;
                    _transport.Input.AdvanceTo(consumed);
                    CommunicationDiagnostics.RecordReceivedBytes(
                        consumedLength,
                        _diagnosticContext);
                    outcome = "success";
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
            !generationToken.IsCancellationRequested)
        {
            // 总超时由 session 转换为 TimeoutException，调用者可与主动取消分别处理。
            outcome = "timeout";
            activity?.SetStatus(ActivityStatusCode.Error, "operation timeout");
            throw new TimeoutException(
                $"通信操作在 {_operationTimeout} 内未完成。",
                ex);
        }
        catch (OperationCanceledException)
        {
            outcome = "canceled";
            activity?.SetStatus(ActivityStatusCode.Error, "operation canceled");
            throw;
        }
        catch (RequestQueueFullException ex)
        {
            outcome = "rejected";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        catch (IOException ex)
        {
            outcome = "faulted";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await TransitionToFaultedAsync(generation, ex).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            outcome = "faulted";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            activity?.SetTag("operation", "request");
            activity?.SetTag("outcome", outcome);
            CommunicationDiagnostics.RecordRequest(
                _timeProvider.GetElapsedTime(startedAt),
                outcome,
                _diagnosticContext);
            if (requestGateEntered)
            {
                _requestGate.Release();
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、StopAsync、WaitAsync、CloseAsync、RecordActiveConnection；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _desiredConnected = false;
        if (_reconnectSupervisor is not null)
        {
            // supervisor 会吞掉自身取消；返回时保证不再访问 transport。
            await _reconnectSupervisor.StopAsync().ConfigureAwait(false);
        }

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
                if (_connectionMetricActive)
                {
                    CommunicationDiagnostics.RecordActiveConnection(-1, _diagnosticContext);
                    _connectionMetricActive = false;
                }
                State = ConnectionState.Disconnected;
                _logger?.LogInformation(
                    "Connection session state changed to {ConnectionState}.",
                    State);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 Write、DisposeCoreAsync；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            // 所有并发调用共享同一个释放过程，避免重复关闭 transport 或释放生命周期锁。
            if (_disposeTask is null)
            {
                // 先发布停止接纳状态，再运行可能同步执行到首个 await 的释放逻辑。
                Volatile.Write(ref _disposeStarted, 1);
                _disposeTask = DisposeCoreAsync();
            }
            return new ValueTask(_disposeTask);
        }
    }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 DisposeCoreAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、DisconnectAsync、WaitAsync、Release、DisposeAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task DisposeCoreAsync()
    {
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
            if (_reconnectSupervisor is not null)
            {
                await _reconnectSupervisor.DisposeAsync().ConfigureAwait(false);
            }
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ThrowIfDisposed。
    /// 正文中的关键协作调用包括 ThrowIf、Read；具体条件和先后次序见方法体。
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed || Volatile.Read(ref _disposeStarted) != 0,
            this);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TransitionToFaultedAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ReferenceEquals、RecordActiveConnection、Release；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    private async ValueTask TransitionToFaultedAsync(
        CancellationTokenSource generation,
        Exception exception)
    {
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (State != ConnectionState.Connected ||
                !ReferenceEquals(_generationCts, generation))
            {
                return;
            }

            State = ConnectionState.Faulted;
            // 连接代际失效后立即取消排队请求，防止它们继续写入已经关闭的字节流。
            _generationCts?.Cancel();
            if (_connectionMetricActive)
            {
                CommunicationDiagnostics.RecordActiveConnection(-1, _diagnosticContext);
                _connectionMetricActive = false;
            }
            _logger?.LogWarning(
                exception,
                "Connection session state changed to {ConnectionState}.",
                State);
            _reconnectSupervisor?.Start();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReconnectOnceAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIfCancellationRequested、CloseAsync、OpenAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async ValueTask<bool> ReconnectOnceAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_desiredConnected || _disposed)
            {
                return true;
            }

            try
            {
                State = ConnectionState.Reconnecting;
                await _transport.CloseAsync(cancellationToken).ConfigureAwait(false);
                await _transport.OpenAsync(cancellationToken).ConfigureAwait(false);
                _generationCts?.Dispose();
                _generationCts = new CancellationTokenSource();
                State = ConnectionState.Connected;
                if (!_connectionMetricActive)
                {
                    CommunicationDiagnostics.RecordActiveConnection(1, _diagnosticContext);
                    _connectionMetricActive = true;
                }
                CommunicationDiagnostics.RecordConnectionAttempt("success", _diagnosticContext);
                _logger?.LogInformation(
                    "Connection session recovered to {ConnectionState}.",
                    State);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 失败状态与 Disconnect 共用同一把锁，不能覆盖手工断开的最终状态。
                State = ConnectionState.Faulted;
                CommunicationDiagnostics.RecordConnectionAttempt("faulted", _diagnosticContext);
                _logger?.LogWarning(ex, "Connection session reconnect attempt failed.");
                return false;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
}
