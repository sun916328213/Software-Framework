using CommunicationNew.Core.EventArgs;

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
        protected CancellationTokenSource? _reconnectCts;
        protected Task? _receiveLoopTask;
        private Task? _reconnectTask;
        private readonly Dictionary<long, Task> _backgroundOperations = new();
        private long _nextBackgroundOperationId;
        protected bool _disposed;

        protected CommunicationDeviceBase(CommunicationParam param)
        {
            Param = param ?? throw new ArgumentNullException(nameof(param));
            DeviceName = param.DeviceName;
            DeviceId = param.DeviceId;
            TimeoutMs = param.TimeoutMs;
        }

        // ===== 事件触发辅助 =====

        protected virtual void OnConnectionStateChanged(bool connected, string? msg = null)
        {
            IsConnected = connected;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(connected, DeviceName, msg));
        }

        protected virtual void OnDataReceived(byte[] data, string? remoteAddress = null)
        {
            DataReceived?.Invoke(this, new DataReceivedEventArgs(data, remoteAddress));
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, new CommunicationErrorEventArgs(ex, DeviceName));
        }

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

        // ===== 自动重连 =====

        protected virtual void StartAutoReconnect(CancellationToken ct)
        {
            if (!Param.AutoReconnect || _reconnectTask is { IsCompleted: false })
            {
                return;
            }

            _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _reconnectCts.Token;

            _reconnectTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !IsConnected)
                {
                    try
                    {
                        await Task.Delay(Param.ReconnectIntervalMs, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) break;
                        await ConnectAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        // 单次重连失败不结束循环，但必须把原因暴露给调用方。
                        OnErrorOccurred(ex);
                    }
                }
            }, token);
        }

        protected virtual void StopAutoReconnect()
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = null;
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

        /// <summary>启动并持有一个短生命周期后台操作，防止异常和停止过程失去所有者。</summary>
        protected void StartBackgroundOperation(Func<Task> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);
            var operationId = Interlocked.Increment(ref _nextBackgroundOperationId);
            var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var task = RunBackgroundOperationAsync(operationId, startSignal.Task, operation);
            lock (SyncRoot)
            {
                _backgroundOperations.Add(operationId, task);
            }
            startSignal.SetResult();
        }

        /// <summary>等待当前设备拥有的所有短生命周期后台操作结束。</summary>
        protected async Task WaitForBackgroundOperationsAsync()
        {
            Task[] tasks;
            lock (SyncRoot)
            {
                tasks = _backgroundOperations.Values.ToArray();
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task RunBackgroundOperationAsync(
            long operationId,
            Task startSignal,
            Func<Task> operation)
        {
            await startSignal.ConfigureAwait(false);
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 设备停止时取消在途操作属于正常生命周期。
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
            finally
            {
                lock (SyncRoot)
                {
                    _backgroundOperations.Remove(operationId);
                }
            }
        }

        // ===== 抽象方法（子类实现）=====

        public abstract Task<bool> ConnectAsync(CancellationToken ct = default);
        public abstract Task DisconnectAsync(CancellationToken ct = default);
        public abstract Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default);
        public abstract Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default);

        // ===== 默认不支持流式收发的协议可重写 =====

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
            if (_disposed) return;
            _receiveLoopCts?.Cancel();
            StopAutoReconnect();
            try
            {
                DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>等待断开与后台重连任务退出后释放资源。</summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _receiveLoopCts?.Cancel();
            StopAutoReconnect();
            try
            {
                await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                await WaitForReceiveLoopAsync().ConfigureAwait(false);
                await WaitForBackgroundOperationsAsync().ConfigureAwait(false);
                if (_reconnectTask is not null)
                {
                    try
                    {
                        await _reconnectTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 释放触发的取消属于正常退出。
                    }
                }
            }
            finally
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();
                StopAutoReconnect();
            }
            _disposed = true;
        }
    }
}
