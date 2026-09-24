using CommunicationNew.Abstractions;
using CommunicationNew.Core.EventArgs;
using CommunicationNew.Runtime;

namespace CommunicationNew.Core
{
    /// <summary>
    /// 通信设备抽象基类 —— 封装公共逻辑（事件触发、重试、超时、资源释放），
    /// 各协议设备继承此类只需关注协议特定的 Connect/Read/Write/Send 实现。
    /// </summary>
    public abstract class CommunicationDeviceBase : ICommunicationDevice
    {
        // ===== 识别与状态 =====

        public virtual string DeviceName { get; set; }
        public string DeviceId { get; protected set; }
        public virtual ProtocolType ProtocolType { get; protected set; }
        public abstract DeviceRole Role { get; }
        public virtual bool IsConnected { get; protected set; }
        public virtual int TimeoutMs { get; set; } = 3000;

        // ===== 事件 =====

        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<DataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred;

        // ===== 保护字段 =====

        protected readonly CommunicationParam Param;
        protected readonly object SyncRoot = new();
        protected CancellationTokenSource? _receiveLoopCts;
        protected Task? _receiveLoopTask;
        private readonly BoundedEventDispatcher<DataReceivedEventArgs> _dataDispatcher;
        private readonly BoundedOperationTracker _backgroundOperations;
        private BoundedReceiveInbox? _receiveInbox;
        private readonly object _disposeSync = new();
        private Task? _disposeTask;
        protected bool _disposed;

        protected CommunicationDeviceBase(CommunicationParam param)
        {
            Param = param ?? throw new ArgumentNullException(nameof(param));
            DeviceName = param.DeviceName;
            DeviceId = param.DeviceId;
            TimeoutMs = param.TimeoutMs;
            _dataDispatcher = new BoundedEventDispatcher<DataReceivedEventArgs>(
                param.EventQueueCapacity,
                args => DataReceived?.Invoke(this, args),
                OnErrorOccurred,
                "data_received");
            _backgroundOperations = new BoundedOperationTracker(
                param.ServerRequestCapacity,
                OnErrorOccurred);
        }

        // ===== 事件触发辅助 =====

        protected virtual void OnConnectionStateChanged(bool connected, string? msg = null)
        {
            IsConnected = connected;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(connected, DeviceName, msg));
        }

        protected virtual void OnDataReceived(byte[] data, string? remoteAddress = null)
        {
            _dataDispatcher.TryPublish(
                new DataReceivedEventArgs(data, remoteAddress),
                CreateEventDiagnosticContext());
        }

        /// <summary>
        /// 将原始字节块或完整数据报写入可靠收件箱，同时保留旧 DataReceived 事件。
        /// 分隔符产生的兼容帧只能调用 <see cref="OnDataReceived"/>，避免在收件箱中重复报文。
        /// </summary>
        protected void OnPacketReceived(byte[] data, string? remoteAddress = null)
        {
            ArgumentNullException.ThrowIfNull(data);
            var context = CreateEventDiagnosticContext();
            (_receiveInbox ?? throw new InvalidOperationException("该设备未启用被动接收能力。"))
                .Publish(new ReceivedPacket(data, remoteAddress), context);
            OnDataReceived(data, remoteAddress);
        }

        /// <summary>为具体原始传输 adapter 启用被动接收能力。</summary>
        private protected void EnableReceiveSource(ReceiveOverflowPolicy overflowPolicy)
        {
            if (_receiveInbox is not null)
            {
                throw new InvalidOperationException("被动接收能力不能重复启用。");
            }

            _receiveInbox = new BoundedReceiveInbox(Param.ReceiveQueueCapacity, overflowPolicy);
        }

        /// <summary>开始新的连接接收代际，使连接建立后立即可以缓存报文。</summary>
        protected long BeginReceiveGeneration() =>
            (_receiveInbox ?? throw new InvalidOperationException("该设备未启用被动接收能力。"))
                .BeginGeneration();

        /// <summary>结束当前连接接收代际并解除挂起的被动读取。</summary>
        protected void EndReceiveGeneration(long generation) =>
            _receiveInbox?.EndGeneration(generation);

        /// <summary>从当前连接代际的收件箱中取出一条报文。</summary>
        protected ValueTask<ReceivedPacket> ReceivePacketAsync(CancellationToken cancellationToken) =>
            (_receiveInbox ?? throw new InvalidOperationException("该设备未启用被动接收能力。"))
                .ReceiveAsync(cancellationToken);

        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, new CommunicationErrorEventArgs(ex, DeviceName));
        }

        private CommunicationDiagnosticContext CreateEventDiagnosticContext() =>
            new(ProtocolType.ToString(), ProtocolType switch
            {
                ProtocolType.Tcp or ProtocolType.ModbusTcp or ProtocolType.MitsubishiMc => "tcp",
                ProtocolType.Udp => "udp",
                ProtocolType.Serial or ProtocolType.ModbusRtu => "serial",
                ProtocolType.Http or ProtocolType.WebApi => "http",
                ProtocolType.OpcUa => "opcua",
                _ => "unknown",
            });

        // ===== 重试包装 =====

        protected async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
        {
            Exception? lastEx = null;
            var retryCount = Math.Max(1, Param.RetryCount);
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    return await action().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    lastEx = ex;
                    OnErrorOccurred(ex);
                    if (i < retryCount - 1)
                        await Task.Delay(Param.RetryDelayMs, ct).ConfigureAwait(false);
                }
            }
            throw lastEx!;
        }

        // ===== 超时包装 =====

        /// <summary>将调用者取消和操作超时传递到实际异步操作。</summary>
        protected async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);
            return await action(cts.Token).ConfigureAwait(false);
        }

        /// <summary>等待设备拥有的接收循环退出；由断开流程在关闭底层句柄后调用。</summary>
        protected async Task WaitForReceiveLoopAsync()
        {
            var task = _receiveLoopTask;
            if (task is null)
            {
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 断开操作主动取消接收循环属于正常生命周期。
            }
            finally
            {
                if (ReferenceEquals(_receiveLoopTask, task))
                {
                    _receiveLoopTask = null;
                }
            }
        }

        /// <summary>
        /// 启动并持有一个短生命周期后台操作；容量不足时抛出异常。
        /// </summary>
        protected void StartBackgroundOperation(Func<Task> operation)
        {
            if (!TryStartBackgroundOperation(operation))
            {
                throw new InvalidOperationException(
                    $"服务端在途请求已达到容量上限 {Param.ServerRequestCapacity}。");
            }
        }

        /// <summary>尝试启动并持有后台操作；容量已满时不调用委托并返回 false。</summary>
        protected bool TryStartBackgroundOperation(Func<Task> operation) =>
            _backgroundOperations.TryStart(operation);

        /// <summary>等待当前设备拥有的所有短生命周期后台操作结束。</summary>
        protected Task WaitForBackgroundOperationsAsync() => _backgroundOperations.DrainAsync();

        // ===== 抽象方法（子类实现）=====

        public abstract Task<bool> ConnectAsync(CancellationToken ct = default);
        public abstract Task DisconnectAsync(CancellationToken ct = default);
        public abstract Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default);
        public abstract Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default);

        // ===== 冻结接口的兼容成员；新调用方通过小型能力接口发现真实语义 =====

        public virtual Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
        {
            // 默认实现：通过 WriteAsync 以空地址发送
            return WriteAsync("", data, ct);
        }

        public virtual Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            throw new NotSupportedException($"{ProtocolType} 协议不支持 SendAndReceive 操作");
        }

        // ===== Dispose =====

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>等待断开与后台任务退出；所有并发调用共享同一个完成过程。</summary>
        public ValueTask DisposeAsync()
        {
            TaskCompletionSource? completion = null;
            Task disposeTask;
            lock (_disposeSync)
            {
                if (_disposeTask is null)
                {
                    completion = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _disposeTask = completion.Task;
                }

                disposeTask = _disposeTask;
            }

            if (completion is not null)
            {
                // 唯一任务已经发布后才调用虚拟方法，避免协议代码重入或阻塞协调锁。
                _ = CompleteDisposeAsync(completion);
            }

            return new ValueTask(disposeTask);
        }

        private async Task CompleteDisposeAsync(TaskCompletionSource completion)
        {
            try
            {
                try
                {
                    _receiveLoopCts?.Cancel();
                    await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                    await WaitForReceiveLoopAsync().ConfigureAwait(false);
                    await WaitForBackgroundOperationsAsync().ConfigureAwait(false);
                }
                finally
                {
                    Dispose(true);
                    GC.SuppressFinalize(this);
                }
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
                return;
            }

            completion.TrySetResult();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();
                _receiveInbox?.Dispose();
                _dataDispatcher.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            _disposed = true;
        }
    }
}
