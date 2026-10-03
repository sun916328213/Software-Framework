// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：BoundedOperationTracker。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime;

/// <summary>
/// 持有有界的短生命周期后台操作，并在排空前统一观察异常。
/// </summary>
internal sealed class BoundedOperationTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<long, Task> _operations = new();
    private readonly int _capacity;
    private readonly Action<Exception> _onError;
    private long _nextOperationId;

    /// <summary>创建指定容量的操作跟踪器。</summary>
    public BoundedOperationTracker(int capacity, Action<Exception> onError)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentNullException.ThrowIfNull(onError);
        _capacity = capacity;
        _onError = onError;
    }

    /// <summary>当前仍在执行的操作数量。</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _operations.Count;
            }
        }
    }

    /// <summary>
    /// 尝试启动操作；容量已满时返回 <see langword="false"/>，且不会调用操作委托。
    /// </summary>
    public bool TryStart(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        TaskCompletionSource startSignal;
        lock (_gate)
        {
            if (_operations.Count >= _capacity)
            {
                return false;
            }

            var operationId = ++_nextOperationId;
            startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            // 先登记再放行，避免同步完成的任务在加入字典前执行清理。
            _operations.Add(operationId, RunAsync(operationId, startSignal.Task, operation));
        }

        startSignal.SetResult();
        return true;
    }

    /// <summary>等待调用时已登记的所有操作结束。</summary>
    public async Task DrainAsync()
    {
        Task[] tasks;
        lock (_gate)
        {
            tasks = _operations.Values.ToArray();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// 运行当前处理流程；对应入口为 RunAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、operation、_onError、Remove；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task RunAsync(long operationId, Task startSignal, Func<Task> operation)
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
            _onError(ex);
        }
        finally
        {
            lock (_gate)
            {
                _operations.Remove(operationId);
            }
        }
    }
}
