// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：CommunicationDeviceBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Abstractions、Communication.Core.EventArgs、Communication.Runtime。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Abstractions;
using Communication.Core.EventArgs;
using Communication.Runtime;

namespace Communication.Runtime.Core
{
    /// <summary>
    /// 通信设备抽象基类 —— 封装公共逻辑（事件触发、重试、超时、资源释放），
    /// 各协议设备继承此类只需关注协议特定的 Connect/Read/Write/Send 实现。
    /// </summary>
    public abstract class CommunicationDeviceBase : ICommunicationDevice, ICommunicationParameterSource
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

        /// <summary>
        /// 初始化 CommunicationDeviceBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <inheritdoc />
        /// <remarks>
        /// 获取当前实现提供的值或对象；对应入口为 GetParameterSnapshot。
        /// </remarks>
        public CommunicationParam GetParameterSnapshot() => Param;

        // ===== 事件触发辅助 =====

        protected virtual void OnConnectionStateChanged(bool connected, string? msg = null)
        {
            IsConnected = connected;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(connected, DeviceName, msg));
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnDataReceived。
        /// 正文中的关键协作调用包括 TryPublish、CreateEventDiagnosticContext；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnErrorOccurred。
        /// </summary>
        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, new CommunicationErrorEventArgs(ex, DeviceName));
        }

        // ===== 失败上下文（T13）=====
        //
        // 冻结的 ICommunicationDevice.ConnectAsync / SendAsync 只能返回 bool，失败原因在
        // 这个边界上被压缩掉了。各设备的 IConnection / IRequestReplyClient 桥若只是抛一句
        // 固定文案的 IOException，调用方就永远看不到「DNS 解析失败」「连接被拒绝」「超时」
        // 这类真正能指导排查的原因，也看不到失败的是哪个端点。
        //
        // 约定：bool 型入口在 catch 里改用 RecordFailure(ex)（它会顺手触发 OnErrorOccurred），
        // 桥再用 CreateFailureException(operation, endpoint) 把 cause 与端点还原出来。

        /// <summary>最近一次 <c>bool</c> 型入口记录下来的原始失败异常；无失败时为 <see langword="null"/>。</summary>
        protected Exception? LastFailure { get; private set; }

        /// <summary>开始一次可能失败的 legacy 操作，清除上一次保留的失败原因，避免把旧失败当成新原因。</summary>
        protected void BeginFailureScope() => LastFailure = null;

        /// <summary>记录失败原异常并上报错误事件；bool 型入口的 catch 分支用它替代直接调用 <see cref="OnErrorOccurred"/>。</summary>
        protected void RecordFailure(Exception ex)
        {
            LastFailure = ex;
            OnErrorOccurred(ex);
        }

        /// <summary>
        /// 把 <c>bool</c> 型失败结果展开为携带原异常 cause 与端点上下文的异常。
        /// </summary>
        /// <param name="operation">操作名，例如「TCP 客户端连接」。</param>
        /// <param name="endpoint">端点描述，例如 <c>127.0.0.1:502</c>；由调用方给出该设备真正连接的目标。</param>
        /// <returns>携带 <see cref="Exception.InnerException"/> 的 <see cref="IOException"/>；无记录时 cause 为 null 且文案如实说明。</returns>
        protected IOException CreateFailureException(string operation, string endpoint)
        {
            var cause = LastFailure;
            var detail = cause is null
                ? "底层未提供失败原因"
                : $"{cause.GetType().Name}: {cause.Message}";
            return new IOException($"{operation}失败（{endpoint}）：{detail}", cause);
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateEventDiagnosticContext。
        /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
        /// </summary>
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
        /// <summary>
        /// 执行断开连接操作；对应入口为 DisconnectAsync。
        /// </summary>
        public abstract Task DisconnectAsync(CancellationToken ct = default);
        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadAsync。
        /// </summary>
        public abstract Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default);
        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// </summary>
        public abstract Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default);

        // ===== 冻结接口的兼容成员；新调用方通过小型能力接口发现真实语义 =====

        public virtual Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
        {
            // 默认实现：通过 WriteAsync 以空地址发送
            return WriteAsync("", data, ct);
        }

        /// <summary>
        /// 执行发送操作；对应入口为 SendAndReceiveAsync。
        /// </summary>
        public virtual Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
        {
            throw new NotSupportedException($"{ProtocolType} 协议不支持 SendAndReceive 操作");
        }

        // ===== Dispose =====

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 GetResult、GetAwaiter、AsTask、DisposeAsync；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CompleteDisposeAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、DisconnectAsync、WaitForReceiveLoopAsync、WaitForBackgroundOperationsAsync、Dispose；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 GetResult、GetAwaiter、AsTask、DisposeAsync；具体条件和先后次序见方法体。
        /// </summary>
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
