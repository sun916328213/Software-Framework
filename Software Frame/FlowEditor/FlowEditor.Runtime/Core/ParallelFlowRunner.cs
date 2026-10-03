// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ParallelFlowLane、ParallelFlowRunner。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
using FlowEditor.Core.Models;

namespace FlowEditor.Runtime;

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
    /// <summary>并发执行全部泳道，并隔离各泳道的引擎调度状态。</summary>
    /// <param name="lanes">需要同时执行的泳道。</param>
    /// <param name="ct">取消令牌。</param>
    /// <param name="log">容器日志回调。</param>
    /// <param name="engine">可选的宿主引擎模板，用于继承日志上下文。</param>
    public static async Task ExecuteAsync<TNode>(
        IReadOnlyList<ParallelFlowLane<TNode>> lanes,
        CancellationToken ct,
        Action<string> log,
        FlowChartExecutionEngine? engine = null)
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

        var tasks = lanes.Select(lane => Task.Run(async () =>
        {
            log($"[并行容器] 分支 [{lane.Name}] 开始");
            try
            {
                var laneEngine = (engine ?? new FlowChartExecutionEngine()).CreateIsolated();
                await laneEngine.ExecuteAsync(
                    lane.Nodes,
                    lane.Connections,
                    ct,
                    message => log($"[并行:{lane.Name}] {message}"));
                log($"[并行容器] 分支 [{lane.Name}] 完成");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log($"[并行容器] 分支 [{lane.Name}] 失败：{ex.Message}；其他分支继续执行");
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
