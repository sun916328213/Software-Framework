namespace FlowEditor.Core;

/// <summary>
/// 循环容器执行模块。循环边界由容器拥有，不在画布中制造普通控制流回边。
/// </summary>
public static class LoopFlowRunner
{
    public static async Task ExecuteAsync<TNode>(
        ParallelFlowLane<TNode> body,
        int iterationCount,
        CancellationToken ct,
        Action<string> log,
        Action<int, int>? onIterationStarted = null)
        where TNode : INodeExecutable
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(log);
        if (iterationCount < 1)
            throw new InvalidOperationException("循环容器的循环次数必须大于零。");
        if (body.Nodes.Count == 0)
            throw new InvalidOperationException("循环容器的循环体没有节点。");

        var engine = new FlowChartExecutionEngine();
        for (var iteration = 1; iteration <= iterationCount; iteration++)
        {
            ct.ThrowIfCancellationRequested();
            onIterationStarted?.Invoke(iteration, iterationCount);
            log($"[循环] 第 {iteration}/{iterationCount} 轮开始");
            await engine.ExecuteAsync(
                body.Nodes,
                body.Connections,
                ct,
                message =>
                {
                    // 外层已经明确记录迭代边界，内部图的编译/入口等基础设施日志
                    // 每轮重复且信息量很低；保留节点、错误和跳过等业务执行日志。
                    if (!message.StartsWith("[引擎]", StringComparison.Ordinal))
                        log(message);
                });
            log($"[循环] 第 {iteration}/{iterationCount} 轮完成");
        }
    }
}
