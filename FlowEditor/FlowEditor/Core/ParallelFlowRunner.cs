using FlowEditor.Models;

namespace FlowEditor.Core;

/// <summary>并行容器中一条已编译泳道。</summary>
public sealed record ParallelFlowLane<TNode>(
    string Id,
    string Name,
    IReadOnlyList<TNode> Nodes,
    IReadOnlyList<ConnectionModel> Connections)
    where TNode : INodeExecutable;

/// <summary>
/// 并行容器执行模块。一次进入会同时启动全部泳道，只有全部泳道结束后才返回。
/// 每条泳道使用独立引擎实例，避免循环序号、汇聚状态等运行态在泳道之间共享。
/// </summary>
public static class ParallelFlowRunner
{
    public static async Task ExecuteAsync<TNode>(
        IReadOnlyList<ParallelFlowLane<TNode>> lanes,
        CancellationToken ct,
        Action<string> log)
        where TNode : INodeExecutable
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(log);

        if (lanes.Count < 2)
            throw new InvalidOperationException("并行容器至少需要两个分支。");

        var emptyLane = lanes.FirstOrDefault(lane => lane.Nodes.Count == 0);
        if (emptyLane != null)
            throw new InvalidOperationException(
                $"并行容器的分支 [{emptyLane.Name}] 没有节点，请配置完整流程。");

        var duplicateId = lanes.GroupBy(lane => lane.Id)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId != null)
            throw new InvalidOperationException($"并行容器存在重复的分支标识 [{duplicateId}]。");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = lanes.Select(lane => Task.Run(async () =>
        {
            log($"[并行容器] 分支 [{lane.Name}] 开始");
            try
            {
                var engine = new FlowChartExecutionEngine();
                await engine.ExecuteAsync(
                    lane.Nodes,
                    lane.Connections,
                    linkedCts.Token,
                    message => log($"[并行:{lane.Name}] {message}"));
                log($"[并行容器] 分支 [{lane.Name}] 完成");
            }
            catch
            {
                linkedCts.Cancel();
                throw;
            }
        }, CancellationToken.None)).ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
    }
}
