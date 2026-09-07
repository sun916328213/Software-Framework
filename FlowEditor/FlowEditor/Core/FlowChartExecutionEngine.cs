using System.Collections.Concurrent;
using System.Diagnostics;
using FlowEditor.Models;
using FlowEditor.Nodes;

namespace FlowEditor.Core;

/// <summary>流程图类型不匹配异常。</summary>
public class FlowChartTypeMismatchException : Exception
{
    public FlowChartTypeMismatchException(string message) : base(message) { }
}

/// <summary>流程图存在环路异常。</summary>
public class FlowChartCycleException : Exception
{
    public FlowChartCycleException(string message) : base(message) { }
}

/// <summary>流程图中未找到开始节点。</summary>
public class FlowChartNoStartNodeException : Exception
{
    public FlowChartNoStartNodeException(string message) : base(message) { }
}

/// <summary>
/// 流程图执行引擎。
/// 统一内核：三种调度模式（DAG 并行 / 环路路径追踪 / 调试串行）共享同一单节点执行核心，
/// 差异仅在下游调度策略。执行前通过 CompiledFlowGraph.Compile 完成图编译（O(N+E)）。
/// 引擎面向 INodeExecutable 执行面调度，不依赖 ViewModel，可在任意宿主中复用。
///
/// 激活边语义（P1）：节点在 ExecuteAsync 中调用 ActivateOutput 声明本次激活的输出端口后，
/// 引擎只沿被激活端口的连线继续调度，未选中分支的子图被整体跳过（状态保持 Idle）；
/// 未声明激活端口的节点保持旧行为（全部出边放行），既有节点零改动兼容。
/// 借此环路可获得自然终止条件（如循环节点计数耗尽后改走 Completed 分支）。
/// </summary>
public class FlowChartExecutionEngine
{
    /// <summary>引用类型计数器，可在 async 方法间安全共享。</summary>
    private sealed class ExecutionCounter
    {
        public int Value;
    }

    /// <summary>
    /// 本次"按一次执行按钮"周期内的全局执行序号（节点 RunSequence 字段的共享累加器）。
    /// 每次 ExecuteAsync 入口重置为 0；每次 ExecuteSingleNodeAsync 推进一次。
    /// 循环回边 BumpRun 不重置，因此循环节点能看到自身在循环内的"被触发序号"，同时循环外的节点 RunSequence 紧接其后。
    /// </summary>
    private int _runSeqCounter;

    private static long Timestamp() => Stopwatch.GetTimestamp();

    private static double ElapsedMs(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    /// <summary>
    /// 执行流程图。
    /// 支持环路：回边自动重新调度；当环内节点（如循环节点）不再激活回边时自然终止。
    /// 用户通过 CancellationToken 停止。
    /// </summary>
    public async Task ExecuteAsync<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections,
        CancellationToken ct,
        Action<string> log,
        Func<TNode, Task>? onBeforeNode = null,
        List<TNode>? executionOrder = null,
        ExecutionJournal? journal = null) where TNode : INodeExecutable
    {
        if (nodes.Count == 0)
        {
            log("[引擎] 画布为空，无需执行");
            return;
        }

        // 重置本轮执行的所有节点 CurrentRunCount；RunSequence 由 Interlocked.Increment 在节点执行入口处推进
        // (重置时机：每次 ExecuteAsync 调用，即"按一次执行按钮"=一个完整执行周期；循环回边 BumpRun 不在此处重置)
        _runSeqCounter = 0;
        foreach (var n in nodes)
        {
            if (n is FlowEditor.ViewModels.NodeViewModel nvm)
                nvm.CurrentRunCount = 0;
            // FlowInstance 直接使用 NodeModel（非 NodeViewModel），同样需要重置周期计数
            else if (n is FlowEditor.Nodes.NodeModel nm)
                nm.CurrentRunCount = 0;
        }

        // 编译期：一次性预计算邻接表、回边、前驱表、拓扑序（O(N+E)）
        var compileStart = Timestamp();
        var graph = CompiledFlowGraph<TNode>.Compile(nodes, connections);
        log($"[引擎] 图编译完成：{nodes.Count} 节点 / {connections.Count} 连线，耗时 {ElapsedMs(compileStart):F2}ms");

        // 调试模式：逐节点串行执行（注：串行模式按拓扑序遍历全图，不做分支过滤）
        if (onBeforeNode != null)
        {
            await ExecuteSequentialAsync(graph, ct, log, onBeforeNode, executionOrder, journal);
            return;
        }

        // 有环路 → 路径追踪执行器（支持激活边终止循环）
        if (graph.HasCycle)
        {
            log("[引擎] 检测到环路，使用路径追踪模式（点击停止按钮终止）");
            await ExecuteCyclePathAsync(graph, ct, log, executionOrder, journal);
            return;
        }

        // 无环路 → 依赖驱动异步执行（DAG 模式，支持并行）
        await ExecuteDagAsync(graph, ct, log, executionOrder, journal);
    }

    #region 单节点执行核心（三种模式共享）

    /// <summary>单节点执行结果。</summary>
    /// <param name="ActivatedIds">显式激活的输出端口 ID；null = 全部出边放行。</param>
    /// <param name="Failed">是否执行失败。</param>
    /// <param name="FailureSwallowed">失败是否被容错策略吞掉（下游照常/按 Error 边调度）。</param>
    private sealed record NodeRunResult(
        IReadOnlySet<string>? ActivatedIds,
        bool Failed,
        bool FailureSwallowed);

    /// <summary>
    /// 执行单个节点：状态切换、日志、输入填充、执行、输出同步、计时、激活端口解析，
    /// 以及超时控制 / 失败重试 / 容错策略（FailFast / ContinueOnError / RouteToErrorPort）。
    /// FailFast 策略或父级取消时抛出异常，由各调度器决定传播；
    /// ContinueOnError 与 RouteToErrorPort 不抛出，通过返回值表达失败。
    /// </summary>
    /// <param name="enforceFailFast">强制快速失败（调试串行模式使用），忽略节点的容错策略。</param>
    private async Task<NodeRunResult> ExecuteSingleNodeAsync<TNode>(
        TNode nodeVm,
        CompiledFlowGraph<TNode> graph,
        CancellationToken ct,
        Action<string> log,
        ConcurrentQueue<TNode>? orderBag,
        Func<string>? onExecuted,
        bool enforceFailFast = false,
        ExecutionJournal? journal = null) where TNode : INodeExecutable
    {
        var startTimestamp = Timestamp();
        nodeVm.Status = NodeStatus.Running;
        orderBag?.Enqueue(nodeVm);
        // 节点级日志统一为「#运行序号 [节点名] 内容」，与 NodeLog 面板/文件格式一致
        Action<string> nodeLog = msg => log($"#{FlowLogContext.RunId} [{nodeVm.Name}] {msg}");
        nodeLog("[开始]");
        journal?.RecordNodeStart(nodeVm.Id, nodeVm.Name);

        // 计数更新：Count 永远累加；CurrentRunCount 在 ExecuteAsync 入口处已重置过；RunSequence 走本轮共享累加器
        // 失败也累加（节点尝试执行了就算"跑过一次"，与 UI 期望一致）
        // INodeExecutable.Count 接口属性覆盖 NodeViewModel 和 NodeModel 两种执行面
        nodeVm.Count++;
        if (nodeVm is FlowEditor.ViewModels.NodeViewModel nvmCounter)
        {
            nvmCounter.CurrentRunCount++;
            nvmCounter.RunSequence = Interlocked.Increment(ref _runSeqCounter);
        }
        else if (nodeVm is FlowEditor.Nodes.NodeModel nmCounter)
        {
            nmCounter.CurrentRunCount++;
            nmCounter.RunSequence = Interlocked.Increment(ref _runSeqCounter);
        }

        // 容错参数（未实现 INodeFaultPolicy 的执行面按默认处理）
        var policyNode = nodeVm as INodeFaultPolicy;
        var errorPolicy = enforceFailFast ? NodeErrorPolicy.FailFast : policyNode?.ErrorPolicy ?? NodeErrorPolicy.FailFast;
        var retryCount = Math.Max(0, policyNode?.RetryCount ?? 0);
        var retryDelayMs = Math.Max(0, policyNode?.RetryDelayMs ?? 0);
        var timeoutMs = Math.Max(0, policyNode?.TimeoutMs ?? 0);
        var maxAttempts = 1 + retryCount;

        try
        {
            ct.ThrowIfCancellationRequested();

            Exception? failure = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (attempt > 1)
                {
                    nodeVm.Status = NodeStatus.Running;
                    if (retryDelayMs > 0)
                        await Task.Delay(retryDelayMs, ct);
                    nodeLog($"[重试] 第 {attempt - 1}/{maxAttempts - 1} 次");
                }

                nodeVm.ResetActivation();
                using var timeoutCts = timeoutMs > 0
                    ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                    : null;
                timeoutCts?.CancelAfter(timeoutMs);

                try
                {
                    FillInputPorts(nodeVm, graph, journal);
                    nodeVm.SyncInputValues();
                    // 节点执行作用域：日志按当前节点路由（FlowLogContext.NodeName）
                    using (FlowLogContext.EnterNode(nodeVm.Name))
                    {
                        await nodeVm.ExecuteAsync(timeoutCts?.Token ?? ct);
                    }
                    nodeVm.SyncOutputValues();
                    failure = null;
                    break;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // 父级取消：不重试、不容错
                }
                catch (OperationCanceledException) when (timeoutCts is { IsCancellationRequested: true })
                {
                    failure = new NodeTimeoutException($"节点 [{nodeVm.Name}] 执行超时（限制 {timeoutMs}ms）");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }

            if (failure == null)
            {
                if (nodeVm.Status != NodeStatus.Error)
                    nodeVm.Status = NodeStatus.Success;

                var elapsedOk = ElapsedMs(startTimestamp);
                nodeVm.LastElapsedMs = (long)elapsedOk;
                var extra = onExecuted?.Invoke() ?? "";
                nodeLog($"[完成] - 耗时 {elapsedOk:F1}ms{extra}");
                journal?.RecordNodeFinish(nodeVm.Id, nodeVm.Name, nodeVm.Status, (long)elapsedOk);

                return new NodeRunResult(ResolveActivatedPortIds(nodeVm, graph), false, false);
            }

            // ── 失败处理（已耗尽重试）──
            nodeVm.LastElapsedMs = (long)ElapsedMs(startTimestamp);
            nodeVm.Status = NodeStatus.Error;
            journal?.RecordNodeFinish(nodeVm.Id, nodeVm.Name, NodeStatus.Error, nodeVm.LastElapsedMs);
            var prefix = failure is FlowChartTypeMismatchException or NodeTimeoutException ? "[错误]" : "[错误]";
            prefix = failure is FlowChartTypeMismatchException ? "[类型错误]" : prefix;

            switch (errorPolicy)
            {
                case NodeErrorPolicy.RouteToErrorPort:
                    var errPortId = TryRouteToErrorPort(nodeVm, graph, failure.Message);
                    if (errPortId != null)
                    {
                        log($"{prefix} {nodeVm.Name}: {failure.Message}");
                        log($"[容错] [{nodeVm.Name}] 失败已路由至 Error 端口，仅沿 Error 连线继续");
                        return new NodeRunResult(new HashSet<string> { errPortId }, true, true);
                    }
                    nodeLog("[容错] 未接线 Error 端口，按继续执行处理");
                    goto case NodeErrorPolicy.ContinueOnError;

                case NodeErrorPolicy.ContinueOnError:
                    nodeLog($"{prefix} {failure.Message}");
                    nodeLog("[容错] 策略为继续执行，下游将以上游遗留输入值继续调度");
                    return new NodeRunResult(null, true, true);

                default: // FailFast
                    nodeLog($"{prefix} {failure.Message}");
                    nodeLog($"[堆栈] {failure.StackTrace}");
                    throw failure;
            }
        }
        catch (OperationCanceledException)
        {
            nodeVm.LastElapsedMs = (long)ElapsedMs(startTimestamp);
            nodeVm.Status = NodeStatus.Error;
            throw;
        }
    }

    /// <summary>查找名为 "Error" 且已有出线的输出端口，将异常消息写入端口值并返回其 ID；不满足时返回 null。</summary>
    private static string? TryRouteToErrorPort<TNode>(
        TNode nodeVm,
        CompiledFlowGraph<TNode> graph,
        string errorMessage) where TNode : INodeExecutable
    {
        var errPort = nodeVm.OutputPorts.FirstOrDefault(p => p.Name == "Error");
        if (errPort == null)
            return null;
        var connected = graph.SuccessorConnections.TryGetValue(nodeVm.Id, out var conns)
                        && conns.Any(c => c.SourcePortId == errPort.Id);
        if (!connected)
            return null;
        errPort.Value = errorMessage; // 携带异常消息供下游节点消费
        return errPort.Id;
    }

    /// <summary>
    /// 将节点声明的激活端口名解析为端口 ID 集合。
    /// 旧图兼容：控制流节点（声明了 ControlFlowPorts）的所有控制流端口均无出线时，
    /// 说明是仅用通用 Next_Out 直通的旧图 → 返回 null 保持全放行。
    /// </summary>
    private static IReadOnlySet<string>? ResolveActivatedPortIds<TNode>(
        TNode nodeVm,
        CompiledFlowGraph<TNode> graph) where TNode : INodeExecutable
    {
        var names = nodeVm.GetActivatedOutputNames();
        if (names == null || names.Count == 0)
            return null;

        HashSet<string>? ids = null;
        foreach (var port in nodeVm.OutputPorts)
        {
            if (names.Contains(port.Name))
                (ids ??= new HashSet<string>()).Add(port.Id);
        }
        if (ids is not { Count: > 0 })
            return null;

        // 控制流端口的旧图回退判定
        var controlNames = nodeVm.ControlFlowPorts;
        if (controlNames is { Count: > 0 })
        {
            var controlIds = nodeVm.OutputPorts
                .Where(p => controlNames.Contains(p.Name))
                .Select(p => p.Id)
                .ToHashSet();
            var controlConnected = graph.SuccessorConnections.TryGetValue(nodeVm.Id, out var outConns)
                                   && outConns.Any(c => controlIds.Contains(c.SourcePortId));
            if (!controlConnected)
                return null;
        }

        return ids;
    }

    #endregion

    #region DAG 调度器（无环路，依赖驱动并行）

    private async Task ExecuteDagAsync<TNode>(
        CompiledFlowGraph<TNode> graph,
        CancellationToken ct,
        Action<string> log,
        List<TNode>? executionOrder,
        ExecutionJournal? journal) where TNode : INodeExecutable
    {
        log($"[引擎] 依赖图构建完成，共 {graph.Nodes.Count} 个节点");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var exceptionList = new ConcurrentBag<Exception>();
        var counter = new ExecutionCounter();
        var totalCount = graph.Nodes.Count;

        var completionSources = graph.Nodes.ToDictionary(
            n => n.Id, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        var remainingPredecessors = new ConcurrentDictionary<string, int>(
            graph.Nodes.ToDictionary(n => n.Id, n => graph.Predecessors[n.Id].Count));

        var failedPredecessorCount = new ConcurrentDictionary<string, int>(
            graph.Nodes.ToDictionary(n => n.Id, _ => 0));

        var orderBag = executionOrder != null ? new ConcurrentQueue<TNode>() : null;

        var entryNodes = graph.EntryNodes.ToList();

        log($"[引擎] 入口节点 {entryNodes.Count} 个，开始依赖驱动异步执行");

        foreach (var entry in entryNodes)
        {
            _ = RunNodeDagAsync(entry, graph, linkedCts.Token, log,
                completionSources, remainingPredecessors,
                failedPredecessorCount, exceptionList,
                counter, totalCount, orderBag, linkedCts, journal);
        }

        try { await Task.WhenAll(completionSources.Values.Select(tcs => tcs.Task)); }
        catch { }

        if (!exceptionList.IsEmpty)
        {
            log($"[引擎] 执行异常，共 {exceptionList.Count} 个错误");
            var firstEx = exceptionList.First();
            if (firstEx is OperationCanceledException) throw firstEx;
            if (firstEx is FlowChartTypeMismatchException) throw firstEx;
            throw new AggregateException("流程执行异常", exceptionList);
        }

        if (orderBag != null) executionOrder!.AddRange(orderBag);
        log($"[引擎] 全部 {totalCount} 个节点执行完成");
    }

    /// <summary>DAG 模式单节点任务：执行核心 + 异常策略收集 + 下游调度。</summary>
    private async Task RunNodeDagAsync<TNode>(
        TNode nodeVm,
        CompiledFlowGraph<TNode> graph,
        CancellationToken ct,
        Action<string> log,
        Dictionary<string, TaskCompletionSource> completionSources,
        ConcurrentDictionary<string, int> remainingPredecessors,
        ConcurrentDictionary<string, int> failedPredecessorCount,
        ConcurrentBag<Exception> exceptionList,
        ExecutionCounter counter,
        int totalCount,
        ConcurrentQueue<TNode>? orderBag,
        CancellationTokenSource linkedCts,
        ExecutionJournal? journal) where TNode : INodeExecutable
    {
        IReadOnlySet<string>? activated = null;
        bool propagateFailure = false;
        NodeRunResult? result = null;
        try
        {
            result = await ExecuteSingleNodeAsync(nodeVm, graph, ct, log, orderBag,
                onExecuted: () =>
                {
                    // 节点本地 Count/CurrentRunCount 已在 ExecuteSingleNodeAsync 入口处累加，此处只推进全局执行进度
                    var executed = Interlocked.Increment(ref counter.Value);
                    return $" ({executed}/{totalCount})";
                },
                journal: journal);
            activated = result.ActivatedIds;
        }
        catch (OperationCanceledException ex)
        {
            exceptionList.Add(ex);
            linkedCts.Cancel();
        }
        catch (FlowChartTypeMismatchException ex)
        {
            exceptionList.Add(ex);
            propagateFailure = true; // FailFast：由下游调度统一标记
        }
        catch (Exception ex)
        {
            exceptionList.Add(ex);
            propagateFailure = true;
        }
        finally
        {
            completionSources[nodeVm.Id].TrySetResult();

            if (graph.SuccessorConnections.TryGetValue(nodeVm.Id, out var outConns))
            {
                foreach (var conn in outConns)
                {
                    ScheduleSuccessorDag(conn, propagateFailure, result, graph, ct, log,
                        completionSources, remainingPredecessors, failedPredecessorCount,
                        exceptionList, counter, totalCount, orderBag, linkedCts, journal);
                }
            }
        }
    }

    /// <summary>
    /// DAG 下游调度：FailFast 失败与激活边过滤（未激活 = 软失败）计入 failedPredecessorCount；
    /// 被容错策略吞掉的失败不标记，下游照常调度（ErrorPort 路由已通过 ActivatedIds 表达走向）。
    /// 前驱归零时按失败/正常分别跳过或调度，共用同一套记账，天然处理多前驱混合场景。
    /// </summary>
    private void ScheduleSuccessorDag<TNode>(
        ConnectionModel conn,
        bool propagateFailure,
        NodeRunResult? result,
        CompiledFlowGraph<TNode> graph,
        CancellationToken ct,
        Action<string> log,
        Dictionary<string, TaskCompletionSource> completionSources,
        ConcurrentDictionary<string, int> remainingPredecessors,
        ConcurrentDictionary<string, int> failedPredecessorCount,
        ConcurrentBag<Exception> exceptionList,
        ExecutionCounter counter,
        int totalCount,
        ConcurrentQueue<TNode>? orderBag,
        CancellationTokenSource linkedCts,
        ExecutionJournal? journal) where TNode : INodeExecutable
    {
        var succId = conn.TargetNodeId;

        if (propagateFailure)
        {
            // FailFast 失败：下游全部标记待跳过
            failedPredecessorCount.AddOrUpdate(succId, 1, (_, v) => v + 1);
        }
        else if (result?.ActivatedIds != null && !result.ActivatedIds.Contains(conn.SourcePortId))
        {
            // 激活边过滤：源端口未被激活 → 该连线视同上游失效。
            // 对成功节点是分支剪枝；对 RouteToErrorPort 失败节点则是"仅沿 Error 边继续"的实现。
            failedPredecessorCount.AddOrUpdate(succId, 1, (_, v) => v + 1);
            log($"[分支] [{graph.NodeMap[conn.SourceNodeId].Name}] 未激活端口连线，[{graph.NodeMap[succId].Name}] 及其下游将被跳过");
        }
        // 其余情况（成功全放行 / ContinueOnError 吞掉且未声明激活）：不标记，下游照常调度

        var remaining = remainingPredecessors.AddOrUpdate(succId, _ => 0, (_, c) => c - 1);
        if (remaining != 0) return;
        if (!graph.NodeMap.TryGetValue(succId, out var succNode)) return;

        if (failedPredecessorCount.GetValueOrDefault(succId, 0) > 0)
        {
            SkipDownstream(succNode, graph,
                completionSources, remainingPredecessors, failedPredecessorCount, log, journal);
        }
        else
        {
            _ = RunNodeDagAsync(succNode, graph, ct, log,
                completionSources, remainingPredecessors,
                failedPredecessorCount, exceptionList,
                counter, totalCount, orderBag, linkedCts, journal);
        }
    }

    /// <summary>标记节点失败，将其所有后继节点的 failedPredecessorCount +1。</summary>
    private static void MarkNodeFailed<TNode>(
        string failedNodeId,
        CompiledFlowGraph<TNode> graph,
        ConcurrentDictionary<string, int> failedPredecessorCount) where TNode : INodeExecutable
    {
        if (graph.Successors.TryGetValue(failedNodeId, out var succList))
        {
            foreach (var succId in succList)
            {
                failedPredecessorCount.AddOrUpdate(succId, 1, (_, v) => v + 1);
            }
        }
    }

    /// <summary>
    /// 递归跳过下游链：解析 completionSource + 标记后继的 failedPredecessorCount。
    /// 被跳过的节点不执行，状态保持 Idle。
    /// </summary>
    private void SkipDownstream<TNode>(
        TNode nodeVm,
        CompiledFlowGraph<TNode> graph,
        Dictionary<string, TaskCompletionSource> completionSources,
        ConcurrentDictionary<string, int> remainingPredecessors,
        ConcurrentDictionary<string, int> failedPredecessorCount,
        Action<string> log,
        ExecutionJournal? journal = null) where TNode : INodeExecutable
    {
        log($"[跳过] {nodeVm.Name} - 上游未执行（失败或分支未选中）");
        journal?.RecordNodeSkipped(nodeVm.Id, nodeVm.Name);

        // 标记该节点"被跳过"，但其后继仍需标记
        MarkNodeFailed(nodeVm.Id, graph, failedPredecessorCount);

        // 解析完成信号
        completionSources[nodeVm.Id].TrySetResult();

        // 递归处理后继
        if (graph.Successors.TryGetValue(nodeVm.Id, out var succList))
        {
            foreach (var succId in succList)
            {
                var remaining = remainingPredecessors.AddOrUpdate(
                    succId, _ => 0, (_, current) => current - 1);

                if (remaining == 0)
                {
                    if (graph.NodeMap.TryGetValue(succId, out var succNode))
                    {
                        SkipDownstream(succNode, graph,
                            completionSources, remainingPredecessors,
                            failedPredecessorCount, log, journal);
                    }
                }
            }
        }
    }

    #endregion

    #region 环路调度器（路径追踪，支持激活边自然终止）

    /// <summary>
    /// 路径追踪执行器：沿连线执行，遇到回边自动循环。
    /// 当环上节点不再激活回边来源端口（如循环计数耗尽）时队列自然排空、执行结束。
    /// </summary>
    private async Task ExecuteCyclePathAsync<TNode>(
        CompiledFlowGraph<TNode> graph,
        CancellationToken ct,
        Action<string> log,
        List<TNode>? executionOrder,
        ExecutionJournal? journal) where TNode : INodeExecutable
    {
        var nodeMap = graph.NodeMap;
        var entryIds = graph.EntryNodes.Select(n => n.Id).ToList();

        log($"[引擎] 入口节点 {entryIds.Count} 个，共 {graph.Nodes.Count} 个节点");

        // 执行队列 + 每节点剩余前驱计数（当前轮次）
        var queue = new Queue<string>();
        var remaining = new Dictionary<string, int>();
        foreach (var entry in entryIds)
        {
            queue.Enqueue(entry);
            remaining[entry] = 0;
        }

        var seq = new int[1];

        // 队列级回边目标去重：循环体存在多条并行末端时，归一化器会为每个末端各合成一条回边，
        // 若不去重则循环头一轮被入队多次 → 计数翻倍、Completed 后残留调度复活循环（无限活锁）。
        // 该集合跨节点持久（区别于旧实现的单次执行局部去重），目标出队执行时移除。
        var pendingBackEdgeTargets = new HashSet<string>();

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var nodeId = queue.Dequeue();
            if (!nodeMap.TryGetValue(nodeId, out var nodeVm))
            {
                pendingBackEdgeTargets.Remove(nodeId);
                continue;
            }

            // 前驱未全部完成 → 重新入队尾部等待
            if (remaining.GetValueOrDefault(nodeId, 0) > 0)
            {
                queue.Enqueue(nodeId);
                continue;
            }

            pendingBackEdgeTargets.Remove(nodeId);

            executionOrder?.Add(nodeVm);

            var result = await ExecuteSingleNodeAsync(nodeVm, graph, ct, log, orderBag: null,
                onExecuted: () =>
                {
                    // 节点本地 Count/CurrentRunCount 已在 ExecuteSingleNodeAsync 入口处累加，此处只推进本轮全局执行序号
                    seq[0]++;
                    return $" (#{seq[0]})";
                },
                journal: journal);
            var activated = result.ActivatedIds;

            // 通知后继（激活边过滤：未激活的出边直接剪枝，包括回边——这是环路自然终止的关键；
            // 容错吞掉的失败经 ErrorPort 路由或全放行表达走向）
            foreach (var conn in graph.SuccessorConnections[nodeId])
            {
                var succId = conn.TargetNodeId;

                if (activated != null && !activated.Contains(conn.SourcePortId))
                {
                    log($"[分支] [{nodeVm.Name}] 未激活该端口连线，剪枝 → [{nodeMap[succId].Name}]");
                    continue;
                }

                if (graph.BackEdges.Contains((conn.SourceNodeId, succId)))
                {
                    // 队列级去重：同一轮内多条并行末端回边指向同一循环头时，只重新调度一次。
                    // （单次执行内的多条回边同样被此集合覆盖，替代旧的局部 rescheduled 方案）
                    if (!pendingBackEdgeTargets.Add(succId)) continue;

                    // 回边 → 进入新一轮：运行序号自增（使循环各轮日志的 #运行序号 可区分），重置目标节点前驱计数后重新调度
                    FlowLogContext.BumpRun();
                    log($"[环路] 重新调度 {nodeMap[succId].Name}");
                    remaining[succId] = 0;
                    nodeMap[succId].Status = NodeStatus.Idle;
                    queue.Enqueue(succId);
                }
                else
                {
                    // 普通边 → 递减后继的前驱计数
                    var rem = remaining.GetValueOrDefault(succId, graph.Predecessors[succId].Count) - 1;
                    remaining[succId] = rem;
                    if (rem <= 0)
                    {
                        remaining[succId] = 0;
                        queue.Enqueue(succId);
                    }
                }
            }
        }

        log($"[引擎] 全部节点执行完成");
    }

    #endregion

    /// <summary>
    /// 检测流程图中是否存在环路（有向图中的环）。
    /// 复用图编译的回边检测（O(N+E)，迭代 DFS 三色标记法）。
    /// </summary>
    public bool HasCycle<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections) where TNode : INodeExecutable
    {
        if (nodes.Count == 0) return false;
        return CompiledFlowGraph<TNode>.Compile(nodes, connections).HasCycle;
    }

    #region 调试串行模式

    /// <summary>
    /// 调试模式：逐节点串行执行（拓扑序）。
    /// 支持环路无限循环：执行完一轮后自动继续下一轮，直到用户停止或环被激活边剪断。
    /// 注：串行调试按整图拓扑序步进，不应用分支过滤。
    /// </summary>
    private async Task ExecuteSequentialAsync<TNode>(
        CompiledFlowGraph<TNode> graph,
        CancellationToken ct,
        Action<string> log,
        Func<TNode, Task> onBeforeNode,
        List<TNode>? executionOrder,
        ExecutionJournal? journal) where TNode : INodeExecutable
    {
        bool hasCycle = graph.HasCycle;
        if (hasCycle)
            log("[引擎] 调试模式: 检测到流程图存在环路，将无限循环执行（点击停止按钮终止）");

        // 拓扑序编译一次，各轮复用
        var sorted = graph.TopologicalOrder;

        int round = 0;
        while (!ct.IsCancellationRequested)
        {
            round++;
            if (hasCycle)
                log($"[引擎] 调试模式: 第 {round} 轮循环开始");

            foreach (var nodeVm in sorted)
            {
                ct.ThrowIfCancellationRequested();
                await onBeforeNode(nodeVm);
                executionOrder?.Add(nodeVm);

                // 调试模式强制 FailFast：失败立即中断步进（重试/超时仍然生效）
                await ExecuteSingleNodeAsync(nodeVm, graph, ct, log, orderBag: null,
                    // 调试模式：节点 Count/CurrentRunCount 已在 ExecuteSingleNodeAsync 入口处累加
                    onExecuted: () => "",
                    enforceFailFast: true,
                    journal: journal);
            }

            // 无环路：一轮就结束
            if (!hasCycle) break;

            // 有环路：重置节点状态准备下一轮
            foreach (var nodeVm in graph.Nodes)
            {
                if (nodeVm.Status != NodeStatus.Error)
                {
                    nodeVm.Status = NodeStatus.Idle;
                    nodeVm.ResetActivation();
                }
            }
        }
    }

    #endregion

    #region 拓扑分层 / 排序（向后兼容）

    /// <summary>
    /// 拓扑分层：返回按依赖深度分组的节点列表。
    /// </summary>
    public List<List<TNode>> TopologicalLayers<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections) where TNode : INodeExecutable
    {
        if (nodes.Count == 0) return [];
        return CompiledFlowGraph<TNode>.Compile(nodes, connections).Layers.Select(l => l.ToList()).ToList();
    }

    /// <summary>
    /// 对所有节点进行拓扑排序（展平为一维列表）。
    /// </summary>
    public List<TNode> TopologicalSort<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections) where TNode : INodeExecutable
    {
        if (nodes.Count == 0) return [];
        return CompiledFlowGraph<TNode>.Compile(nodes, connections).TopologicalOrder;
    }

    #endregion

    #region 数据传递

    /// <summary>
    /// 将上游节点输出端口的值填充到目标节点的输入端口。
    /// 入边通过编译图的 IncomingConnections 索引 O(1) 获取。
    /// </summary>
    private void FillInputPorts<TNode>(
        TNode targetNode,
        CompiledFlowGraph<TNode> graph,
        ExecutionJournal? journal = null) where TNode : INodeExecutable
    {
        var incomingConns = graph.IncomingConnections[targetNode.Id];

        foreach (var conn in incomingConns)
        {
            if (!graph.NodeMap.TryGetValue(conn.SourceNodeId, out var sourceNode))
                continue;
            var sourcePort = sourceNode.FindPortById(conn.SourcePortId);
            var targetPort = targetNode.FindPortById(conn.TargetPortId);
            if (sourcePort == null || targetPort == null)
                continue;

            // 1. 检查端口声明类型是否兼容
            if (!targetPort.IsTypeCompatibleWith(sourcePort.DataType))
            {
                throw new FlowChartTypeMismatchException(
                    $"源端口 [{sourceNode.Name}].{sourcePort.Name}({sourcePort.DataType}) " +
                    $"与目标端口 [{targetNode.Name}].{targetPort.Name}({targetPort.DataType}) 声明类型不兼容");
            }

            // 2. 检查端口运行时值类型是否兼容
            if (!targetPort.IsCompatible(sourcePort.Value))
            {
                var sourceValType = sourcePort.Value?.GetType().Name ?? "null";
                throw new FlowChartTypeMismatchException(
                    $"源端口 [{sourceNode.Name}].{sourcePort.Name} 的值 " +
                    $"'{sourcePort.Value}'（运行时类型: {sourceValType}）" +
                    $"无法传入目标端口 [{targetNode.Name}].{targetPort.Name}({targetPort.DataType})");
            }

            targetPort.Value = sourcePort.Value;
            journal?.RecordEdgeTransfer(conn.Id, sourcePort.Value); // P4 数据追踪：记录每条连线的实际传值
        }
    }

    #endregion
}
