// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DataPointAutoReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Services、Communication.Runtime.Core、Communication.Core.Abstractions、Communication.Core.EventArgs、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Services;
using Communication.Runtime.Core;
using Communication.Core.Abstractions;
using Communication.Core.EventArgs;
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Communication.Runtime;

namespace Communication.Runtime.Core.Services
{
    /// <summary>
    /// 数据点自动轮询读取器 —— 后台按点位周期读取所有可读数据点，
    /// 值变化时触发 DataPointChanged 事件。读取逻辑只依赖数据点读取 seam。
    /// </summary>
    public class DataPointAutoReader : IAutoReader
    {
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private readonly IDataPointBatchReader _reader;
        private readonly string _diagnosticProtocol;
        private readonly DataPointCollection _collection;
        private readonly TimeProvider _timeProvider;
        private readonly object _lock = new();
        private Task? _disposeTask;
        private volatile bool _disposeStarted;
        private int _intervalMs = 1000;
        private int _eventQueueCapacity = 256;

        public bool IsRunning { get; private set; }

        /// <inheritdoc />
        public int IntervalMs
        {
            get => _intervalMs;
            set => _intervalMs = value > 0
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "默认轮询周期必须大于零。");
        }

        /// <summary>
        /// 后台扫描的点位变化队列容量；同一点位等待消费时只保留最新通知。
        /// 只能在轮询停止时修改。
        /// </summary>
        public int EventQueueCapacity
        {
            get => _eventQueueCapacity;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                lock (_lock)
                {
                    if (IsRunning)
                    {
                        throw new InvalidOperationException("轮询运行期间不能修改事件队列容量。");
                    }

                    _eventQueueCapacity = value;
                }
            }
        }

        public event EventHandler<DataPointChangedEventArgs>? DataPointChanged;

        /// <summary>读取失败回调（参数：数据点名，异常）</summary>
        public event EventHandler<(string name, Exception ex)>? ReadFailed;

        /// <summary>
        /// 初始化 DataPointAutoReader 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DataPointAutoReader(ICommunicationDevice device, DataPointCollection collection)
            : this(device, collection, TimeProvider.System)
        {
        }

        /// <summary>使用可替换时钟创建数据点轮询器。</summary>
        public DataPointAutoReader(
            ICommunicationDevice device,
            DataPointCollection collection,
            TimeProvider timeProvider)
            : this(
                CreateReader(device),
                collection,
                device.ProtocolType.ToString(),
                timeProvider)
        {
        }

        /// <summary>
        /// 使用最小按地址读取能力创建轮询器；适合无需暴露旧设备生命周期和写入能力的 adapter。
        /// </summary>
        /// <param name="reader">负责按协议地址读取原始数据的 adapter。</param>
        /// <param name="collection">由轮询器调度和更新的数据点集合。</param>
        /// <param name="diagnosticProtocol">写入诊断标签的低基数协议名称。</param>
        public DataPointAutoReader(
            IAddressReader reader,
            DataPointCollection collection,
            string diagnosticProtocol)
            : this(
                new AddressDataPointReaderAdapter(reader),
                collection,
                diagnosticProtocol,
                TimeProvider.System)
        {
        }

        /// <summary>
        /// 使用数据点读取 seam 创建轮询器，不要求 adapter 实现旧设备的连接、写入或原始收发成员。
        /// </summary>
        /// <param name="reader">负责读取一组数据点的 adapter。</param>
        /// <param name="collection">由轮询器调度和更新的数据点集合。</param>
        /// <param name="diagnosticProtocol">写入诊断标签的低基数协议名称。</param>
        /// <param name="timeProvider">提供扫描调度与采样时间的时钟。</param>
        public DataPointAutoReader(
            IDataPointBatchReader reader,
            DataPointCollection collection,
            string diagnosticProtocol,
            TimeProvider timeProvider)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticProtocol);
            _diagnosticProtocol = diagnosticProtocol;
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Start。
        /// 正文中的关键协作调用包括 ThrowIfDisposed、RunLoopAsync；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                if (IsRunning) return;
                _cts = new CancellationTokenSource();
                IsRunning = true;
                _loopTask = RunLoopAsync(_cts.Token);
            }
        }

        /// <summary>
        /// 兼容旧同步调用的停止入口。
        /// 新代码应使用 <see cref="StopAsync"/>，避免阻塞调用线程。
        /// </summary>
        public void Stop()
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>取消轮询，并在当前读取与循环退出后返回。</summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            Task? loopTask;
            CancellationTokenSource? cts;
            lock (_lock)
            {
                loopTask = _loopTask;
                cts = _cts;
                if (loopTask is null || cts is null)
                {
                    IsRunning = false;
                    return;
                }

                cts.Cancel();
            }

            try
            {
                await loopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // 轮询自身的停止取消属于正常生命周期。
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_loopTask, loopTask))
                    {
                        _loopTask = null;
                        _cts = null;
                        IsRunning = false;
                    }
                }

                cts.Dispose();
            }
        }

        /// <summary>
        /// 运行当前处理流程；对应入口为 RunLoopAsync。
        /// 正文中的关键协作调用包括 Plan、GetUtcNow、FromMilliseconds、ConfigureAwait、PollPointsAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private async Task RunLoopAsync(CancellationToken ct)
        {
            await using var eventDispatcher = new LatestValueDispatcher<string, DataPointChangedEventArgs>(
                EventQueueCapacity,
                args => DataPointChanged?.Invoke(this, args),
                ex => ReadFailed?.Invoke(this, ("<data-point-subscriber>", ex)),
                "data_point_changed",
                new CommunicationDiagnosticContext(_diagnosticProtocol, "poller"));

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var plan = DataPointScanPlanner.Plan(
                        _collection,
                        _timeProvider.GetUtcNow(),
                        TimeSpan.FromMilliseconds(IntervalMs));
                    if (plan.DuePoints.Count > 0)
                    {
                        await PollPointsAsync(plan.DuePoints, ct, eventDispatcher).ConfigureAwait(false);
                        plan = DataPointScanPlanner.Plan(
                            _collection,
                            _timeProvider.GetUtcNow(),
                            TimeSpan.FromMilliseconds(IntervalMs));
                    }

                    await Task.Delay(
                            plan.DelayUntilNextScan,
                            _timeProvider,
                            ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ReadFailed?.Invoke(this, ("<poll-loop>", ex));
                }
            }

            lock (_lock)
            {
                IsRunning = false;
            }
        }

        /// <summary>执行一轮轮询（可复用给手动刷新）</summary>
        public async Task PollOnceAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            var readablePoints = _collection
                .Where(static point => point.Readable)
                .ToArray();
            await PollPointsAsync(readablePoints, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 PollPointsAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、PollBatchAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private async Task PollPointsAsync(
            IReadOnlyList<IDataPoint> readablePoints,
            CancellationToken ct,
            LatestValueDispatcher<string, DataPointChangedEventArgs>? eventDispatcher = null)
        {
            await PollBatchAsync(
                _reader,
                readablePoints,
                ct,
                eventDispatcher).ConfigureAwait(false);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 PollBatchAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、ReadPointsAsync、PublishFailure、PublishSuccess；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private async Task PollBatchAsync(
            IDataPointBatchReader batchReader,
            IReadOnlyList<IDataPoint> points,
            CancellationToken ct,
            LatestValueDispatcher<string, DataPointChangedEventArgs>? eventDispatcher)
        {
            IReadOnlyList<DataPointReadResult> results;
            try
            {
                results = await batchReader.ReadPointsAsync(points, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                foreach (var point in points)
                {
                    PublishFailure(point, ex);
                }
                return;
            }

            foreach (var result in results)
            {
                if (result.Error is not null)
                {
                    PublishFailure(result.Point, result.Error);
                    continue;
                }

                try
                {
                    PublishSuccess(result.Point, result.RawValue!, ct, eventDispatcher);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    PublishFailure(result.Point, ex);
                }
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 PublishSuccess。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、ApplyRead、GetUtcNow、Equals、TryPublish；具体条件和先后次序见方法体。
        /// </summary>
        private void PublishSuccess(
            IDataPoint point,
            byte[] bytes,
            CancellationToken ct,
            LatestValueDispatcher<string, DataPointChangedEventArgs>? eventDispatcher)
        {
            ct.ThrowIfCancellationRequested();
            var oldUpdate = point.Current;
            point.ApplyRead(bytes, _timeProvider.GetUtcNow());
            var newUpdate = point.Current;
            if (!Equals(oldUpdate.Value, newUpdate.Value))
            {
                var args = new DataPointChangedEventArgs(
                    point,
                    oldUpdate.Value,
                    newUpdate.Value);
                if (eventDispatcher is null)
                {
                    DataPointChanged?.Invoke(this, args);
                }
                else
                {
                    eventDispatcher.TryPublish(point.Name, args);
                }
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 PublishFailure。
        /// 正文中的关键协作调用包括 ApplyFailure、GetUtcNow；具体条件和先后次序见方法体。
        /// </summary>
        private void PublishFailure(IDataPoint point, Exception error)
        {
            point.ApplyFailure(error, _timeProvider.GetUtcNow());
            ReadFailed?.Invoke(this, (point.Name, error));
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateReader。
        /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
        /// </summary>
        private static IDataPointBatchReader CreateReader(ICommunicationDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            if (device is IDataPointBatchReader batchReader)
            {
                return batchReader;
            }

            var addressReader = device as IAddressReader ?? new LegacyAddressReaderAdapter(device);
            return new AddressDataPointReaderAdapter(addressReader);
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Stop、SuppressFinalize；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        /// <summary>停止轮询并等待退出；并发释放调用共享同一个完成过程。</summary>
        public ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                if (_disposeTask is null)
                {
                    // 先发布终态，防止停止期间再次接纳后台或手动轮询。
                    _disposeStarted = true;
                    _disposeTask = DisposeCoreAsync();
                }

                return new ValueTask(_disposeTask);
            }
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 DisposeCoreAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、StopAsync、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private async Task DisposeCoreAsync()
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ThrowIfDisposed。
        /// 正文中的关键协作调用包括 ThrowIf；具体条件和先后次序见方法体。
        /// </summary>
        private void ThrowIfDisposed() =>
            ObjectDisposedException.ThrowIf(_disposeStarted, this);
    }
}
