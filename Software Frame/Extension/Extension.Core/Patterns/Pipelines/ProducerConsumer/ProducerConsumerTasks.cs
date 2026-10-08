using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Extension.Patterns.Pipelines.ProducerConsumer;

/// <summary>投递生产/消费任务，保留各模式的任务异常策略。</summary>
internal static class ProducerConsumerTasks
{
    /// <summary>用关联令牌投递所有任务并等待它们结束。</summary>
    internal static async Task RunAsync(
        int producerCount, int consumerCount,
        Func<int, CancellationToken, Task> produce,
        Func<int, CancellationToken, Task> consume,
        bool rethrowErrors, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = new List<Task>();
        Start(producerCount, "Producer", produce);
        Start(consumerCount, "Consumer", consume);
        await Task.WhenAll(tasks);

        void Start(int count, string role, Func<int, CancellationToken, Task> run)
        {
            for (int index = 0; index < count; index++)
            {
                int id = index + 1;
                tasks.Add(Task.Run(async () =>
                {
                    try { await run(id, cts.Token); }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{role}-{id}] 发生异常: {ex.Message}");
                        if (rethrowErrors) throw;
                    }
                }, cts.Token));
            }
        }
    }
}
