// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：AsyncDisposeOnce。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime.Transports;

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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CompleteAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、disposeAsync、TrySetResult、TrySetException；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
