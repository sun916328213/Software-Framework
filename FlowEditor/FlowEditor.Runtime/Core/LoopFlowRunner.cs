namespace FlowEditor.Core;

/// <summary>
/// 循环容器执行模块。循环边界由容器拥有，不在画布中制造普通控制流回边。
/// </summary>
public static class LoopFlowRunner
{
    /// <summary>按配置次数和轮间延时顺序执行循环体。</summary>
    /// <param name="body">已编译的循环体。</param>
    /// <param name="iterationCount">循环次数；-1 表示无限循环。</param>
    /// <param name="ct">取消令牌。</param>
    /// <param name="log">执行日志回调。</param>
    /// <param name="onIterationStarted">每轮开始前的可选回调。</param>
    /// <param name="iterationDelayMs">两轮之间的等待毫秒数。</param>
    /// <param name="startingIteration">恢复执行时的起始轮次。</param>
    /// <param name="engine">可选的宿主引擎，用于继承日志上下文。</param>
    public static async Task ExecuteAsync<TNode>(
        ParallelFlowLane<TNode> body,
        int iterationCount,
        CancellationToken ct,
        Action<string> log,
        Action<int, int>? onIterationStarted = null,
        int iterationDelayMs = 0,
        int startingIteration = 1,
        FlowChartExecutionEngine? engine = null)
        where TNode : INodeExecutable
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(log);
        if (iterationCount != -1 && iterationCount < 1)
            throw new InvalidOperationException("循环容器的循环次数必须为 -1（无限循环）或大于零。");
        if (iterationDelayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(iterationDelayMs), "循环延时不能小于零。");
        if (startingIteration < 1)
            throw new ArgumentOutOfRangeException(nameof(startingIteration), "起始轮次必须大于零。");
        if (body.Nodes.Count == 0)
            throw new InvalidOperationException("循环容器的循环体没有节点。");

        engine ??= new FlowChartExecutionEngine();
        for (var iteration = startingIteration;
             iterationCount == -1 || iteration <= iterationCount;
             iteration++)
        {
            ct.ThrowIfCancellationRequested();
            onIterationStarted?.Invoke(iteration, iterationCount);
            var totalText = iterationCount == -1 ? "∞" : iterationCount.ToString();
            log($"[循环] 第 {iteration}/{totalText} 轮开始");
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
            log($"[循环] 第 {iteration}/{totalText} 轮完成");
            if (iterationDelayMs > 0 && (iterationCount == -1 || iteration < iterationCount))
                await Task.Delay(iterationDelayMs, ct);
        }
    }
}
