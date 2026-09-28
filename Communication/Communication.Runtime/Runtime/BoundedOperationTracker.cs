namespace CommunicationNew.Runtime;

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
