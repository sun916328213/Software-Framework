namespace CommunicationNew.Transports;

/// <summary>
/// 把异步释放实现收敛为唯一任务，使所有并发调用共享完成结果与异常。
/// 该 module 仅用于 transport 内部生命周期，不进入公共 seam。
/// </summary>
internal sealed class AsyncDisposeOnce(Func<Task> disposeAsync)
{
    private readonly object _sync = new();
    private Task? _disposeTask;

    /// <summary>释放任务是否已经登记；一旦为 true 就不会恢复。</summary>
    public bool IsStarted => Volatile.Read(ref _disposeTask) is not null;

    /// <summary>启动一次释放，或返回已经启动的同一个释放任务。</summary>
    public ValueTask RunAsync()
    {
        TaskCompletionSource? completion = null;
        Task disposeTask;
        lock (_sync)
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
            _ = CompleteAsync(completion);
        }

        return new ValueTask(disposeTask);
    }

    private async Task CompleteAsync(TaskCompletionSource completion)
    {
        try
        {
            await disposeAsync().ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
    }
}
