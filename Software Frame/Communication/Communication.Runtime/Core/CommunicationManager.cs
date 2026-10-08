// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：CommunicationManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.ICommunicationDeviceFactory、Communication.Core、System.Collections.Concurrent、Communication.Core.Abstractions、Communication.Runtime.Composition。导入不代表所有类型都会在运行时加载。
using ICommunicationDeviceFactory = Communication.Core.ICommunicationDeviceFactory;
using Communication.Core;
using System.Collections.Concurrent;
using Communication.Core.Abstractions;
using Communication.Runtime.Composition;

namespace Communication.Runtime.Core
{
    /// <summary>
    /// 通信设备统一管理器 —— 管理所有通信设备的生命周期。
    /// 外部系统通过此管理器获取设备，无需关心具体协议实现。
    /// </summary>
    public class CommunicationManager : ICommunicationManager
    {
        private readonly ConcurrentDictionary<string, ICommunicationDevice> _devices = new();
        // 字典负责设备索引的并发访问；创建、移除、批量操作及释放的协调仍依赖后面的生命周期锁。
        // 不能因为使用了并发字典，就把整个设备创建与释放流程视为自动具备原子性。
        private readonly ICommunicationDeviceFactory _factory;
        private readonly object _lifecycleGate = new();
        private readonly HashSet<Task> _pendingBatchOperations = [];
        private readonly HashSet<Task> _pendingRemovals = [];
        private Task _batchOperationTail = Task.CompletedTask;
        private Task? _disposeTask;
        private bool _disposed;

        /// <summary>
        /// 使用可注入的设备 factory 创建设备管理器。
        /// factory seam 位于 Core，调用方无需引用默认协议组合实现。
        /// </summary>
        /// <param name="factory">负责根据参数创建设备的 factory adapter。</param>
        public CommunicationManager(ICommunicationDeviceFactory factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            _factory = factory;
        }

        /// <summary>所有已注册的设备</summary>
        public IReadOnlyCollection<ICommunicationDevice> Devices => _devices.Values.ToList().AsReadOnly();

        /// <summary>
        /// 创建并注册一个通信设备
        /// </summary>
        public ICommunicationDevice CreateDevice(CommunicationParam param)
        {
            lock (_lifecycleGate)
            {
                ThrowIfDisposed();
            }

            var device = _factory.Create(param);
            var deviceId = device.DeviceId;
            bool managerDisposed;
            lock (_lifecycleGate)
            {
                managerDisposed = _disposed;
                if (!managerDisposed && _devices.TryAdd(deviceId, device))
                {
                    return device;
                }
            }

            // factory 创建的实例尚未交给调用方，接管失败时由管理器立即回收，避免资源泄漏。
            device.Dispose();
            if (managerDisposed)
            {
                throw new ObjectDisposedException(nameof(CommunicationManager));
            }

            throw new InvalidOperationException($"设备 ID '{deviceId}' 已注册。");
        }

        /// <summary>
        /// 注册已有设备
        /// </summary>
        public void RegisterDevice(ICommunicationDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            lock (_lifecycleGate)
            {
                ThrowIfDisposed();
                if (!_devices.TryAdd(device.DeviceId, device))
                {
                    // 注册失败表示所有权没有转移，传入实例仍由调用方负责释放。
                    throw new InvalidOperationException($"设备 ID '{device.DeviceId}' 已注册。");
                }
            }
        }

        /// <summary>
        /// 按ID获取设备
        /// </summary>
        public ICommunicationDevice? GetDevice(string deviceId)
        {
            return _devices.TryGetValue(deviceId, out var device) ? device : null;
        }

        /// <summary>
        /// 按名称获取设备
        /// </summary>
        public ICommunicationDevice? GetDeviceByName(string name)
        {
            return _devices.Values.FirstOrDefault(d => d.DeviceName == name);
        }

        /// <summary>
        /// 按协议类型获取设备列表
        /// </summary>
        public List<ICommunicationDevice> GetDevicesByType(ProtocolType type)
        {
            return _devices.Values.Where(d => d.ProtocolType == type).ToList();
        }

        /// <summary>
        /// 移除并释放设备
        /// </summary>
        public Task<bool> RemoveDeviceAsync(string deviceId)
        {
            ICommunicationDevice? device;
            Task[] pendingBatchOperations;
            TaskCompletionSource<bool> completion;
            lock (_lifecycleGate)
            {
                ThrowIfDisposed();
                if (!_devices.TryRemove(deviceId, out device))
                {
                    return Task.FromResult(false);
                }

                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingRemovals.Add(completion.Task);
                pendingBatchOperations = _pendingBatchOperations.ToArray();
            }

            // 在生命周期门内登记清理任务，但在锁外执行设备代码，避免阻塞其他管理操作。
            _ = CompleteRemovalAsync(device, pendingBatchOperations, completion);
            return completion.Task;
        }

        /// <summary>
        /// 连接所有设备
        /// </summary>
        public Task ConnectAllAsync(CancellationToken ct = default) =>
            StartBatchOperation(device => ConnectDeviceAsync(device, ct));

        /// <summary>
        /// 断开所有设备
        /// </summary>
        /// <param name="ct">用于取消尚未完成的设备断开操作。</param>
        public Task DisconnectAllAsync(CancellationToken ct = default) =>
            StartBatchOperation(device => DisconnectDeviceAsync(device, ct));

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 GetResult、GetAwaiter、AsTask、DisposeAsync、SuppressFinalize；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 断开并异步释放管理器拥有的全部设备。
        /// 并发调用共享同一个完成任务，只有全部设备处理结束后才返回。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            ICommunicationDevice[] devices;
            Task[] pendingBatchOperations;
            Task[] pendingRemovals;
            TaskCompletionSource completion;
            lock (_lifecycleGate)
            {
                if (_disposeTask is not null)
                {
                    return new ValueTask(_disposeTask);
                }

                _disposed = true;
                devices = _devices.Values.ToArray();
                _devices.Clear();
                pendingBatchOperations = _pendingBatchOperations.ToArray();
                pendingRemovals = _pendingRemovals.ToArray();
                completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = completion.Task;
            }

            // 先发布共享任务再执行外部设备代码，避免重入或并发 Dispose 提前返回。
            _ = CompleteDisposeAsync(
                devices,
                pendingBatchOperations,
                pendingRemovals,
                completion);
            return new ValueTask(completion.Task);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartBatchOperation。
        /// 正文中的关键协作调用包括 ThrowIfDisposed、ToArray、Add、CompleteBatchOperationAsync；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        private Task StartBatchOperation(Func<ICommunicationDevice, Task> operation)
        {
            ICommunicationDevice[] devices;
            Task precedingOperation;
            TaskCompletionSource completion;
            lock (_lifecycleGate)
            {
                ThrowIfDisposed();
                devices = _devices.Values.ToArray();
                precedingOperation = _batchOperationTail;
                completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingBatchOperations.Add(completion.Task);
                _batchOperationTail = completion.Task;
            }

            // 先登记任务再调用设备，确保随后开始的整体释放一定能观察并等待本次操作。
            _ = CompleteBatchOperationAsync(
                devices,
                precedingOperation,
                operation,
                completion);
            return completion.Task;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CompleteBatchOperationAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitForBatchOperationAsync、WhenAll、Select、SetResult；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private async Task CompleteBatchOperationAsync(
            IReadOnlyList<ICommunicationDevice> devices,
            Task precedingOperation,
            Func<ICommunicationDevice, Task> operation,
            TaskCompletionSource completion)
        {
            Task? batchOperation = null;
            try
            {
                // 批量生命周期命令按提交顺序执行；前序失败不应阻止后续清理命令运行。
                await WaitForBatchOperationAsync(precedingOperation).ConfigureAwait(false);
                batchOperation = Task.WhenAll(devices.Select(operation));
                await batchOperation.ConfigureAwait(false);
                completion.SetResult();
            }
            catch (OperationCanceledException ex)
            {
                completion.SetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                var failures = batchOperation?.Exception?.Flatten().InnerExceptions;
                if (failures is { Count: > 1 })
                {
                    // await Task.WhenAll 默认只抛出一个异常；在 manager seam 保留全部设备故障。
                    completion.SetException(
                        new AggregateException("一个或多个通信设备操作失败。", failures));
                }
                else
                {
                    completion.SetException(ex);
                }
            }
            finally
            {
                lock (_lifecycleGate)
                {
                    _pendingBatchOperations.Remove(completion.Task);
                }
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CompleteRemovalAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitForBatchOperationsAsync、DisposeAsync、SetResult、SetException；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private async Task CompleteRemovalAsync(
            ICommunicationDevice device,
            IReadOnlyList<Task> pendingBatchOperations,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                // 设备可能仍在此前已接纳的批量操作中；先等待操作退出，再执行最终释放。
                await WaitForBatchOperationsAsync(pendingBatchOperations).ConfigureAwait(false);
                await device.DisposeAsync().ConfigureAwait(false);
                completion.SetResult(true);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                lock (_lifecycleGate)
                {
                    _pendingRemovals.Remove(completion.Task);
                }
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CompleteDisposeAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitForBatchOperationsAsync、Add、DisposeAsync、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private async Task CompleteDisposeAsync(
            IReadOnlyList<ICommunicationDevice> devices,
            IReadOnlyList<Task> pendingBatchOperations,
            IReadOnlyList<Task> pendingRemovals,
            TaskCompletionSource completion)
        {
            var errors = new List<Exception>();
            await WaitForBatchOperationsAsync(pendingBatchOperations).ConfigureAwait(false);

            foreach (var removal in pendingRemovals)
            {
                try
                {
                    // 已移出字典的设备仍由管理器负责，整体释放必须等待其清理完成。
                    await removal.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }

            foreach (var device in devices)
            {
                try
                {
                    // IAsyncDisposable 是设备最终关闭 seam；基类会在其中断开连接并等待后台任务。
                    await device.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // 先释放其余设备，结束后将全部错误交给调用方处理。
                    errors.Add(ex);
                }
            }

            GC.SuppressFinalize(this);
            if (errors.Count > 0)
            {
                completion.SetException(
                    new AggregateException("一个或多个通信设备释放失败。", errors));
                return;
            }

            completion.SetResult();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 WaitForBatchOperationsAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、WaitForBatchOperationAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private static async Task WaitForBatchOperationsAsync(
            IReadOnlyList<Task> pendingBatchOperations)
        {
            foreach (var operation in pendingBatchOperations)
            {
                await WaitForBatchOperationAsync(operation).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 WaitForBatchOperationAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static async Task WaitForBatchOperationAsync(Task operation)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            catch
            {
                // 批量操作的原调用负责观察失败；后续生命周期流程只等待它停止。
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ThrowIfDisposed。
        /// 正文中的关键协作调用包括 ThrowIf；具体条件和先后次序见方法体。
        /// </summary>
        private void ThrowIfDisposed() =>
            ObjectDisposedException.ThrowIf(_disposed, this);

        /// <summary>
        /// 发起连接操作；对应入口为 ConnectDeviceAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private static async Task ConnectDeviceAsync(
            ICommunicationDevice device,
            CancellationToken cancellationToken)
        {
            if (device is IConnection connection)
            {
                await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            // 旧接口用 false 表示失败；在 manager seam 将其归一为异常，避免批量启动假成功。
            if (!await device.ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new IOException(
                    $"设备 '{device.DeviceName}' ({device.DeviceId}) 连接失败。");
            }
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 DisconnectDeviceAsync。
        /// 正文中的关键协作调用包括 ConfigureAwait、DisconnectAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private static async Task DisconnectDeviceAsync(
            ICommunicationDevice device,
            CancellationToken cancellationToken)
        {
            // async 适配层会把 adapter 的同步异常转换为独立故障任务，避免中断其余设备枚举。
            if (device is IConnection connection)
            {
                await connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await device.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
