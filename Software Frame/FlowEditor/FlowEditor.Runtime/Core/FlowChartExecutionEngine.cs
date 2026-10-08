// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartTypeMismatchException、FlowChartExecutionEngine、ExecutionCounter、NodeRunResult。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core、System.Collections.Concurrent、System.Diagnostics、FlowEditor.Core.Models、FlowEditor.Core.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
using FlowEditor.Core.Models;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Runtime;

/// <summary>流程图类型不匹配异常。</summary>
public class FlowChartTypeMismatchException : Exception
{
    /// <summary>使用描述不兼容端口或值的消息创建异常。</summary>
    /// <param name="message">类型不匹配的诊断消息。</param>
    public FlowChartTypeMismatchException(string message) : base(message) { }
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
    private readonly IExecutionLogContext _logContext;

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

    /// <summary>创建执行引擎，并使用宿主日志上下文或无头默认上下文。</summary>
    /// <param name="logContext">宿主日志上下文；省略时不产生上下文副作用。</param>
    public FlowChartExecutionEngine(IExecutionLogContext? logContext = null)
    {
        _logContext = logContext ?? NullExecutionLogContext.Instance;
    }

    /// <summary>
    /// 创建共享同一日志上下文、但拥有独立调度状态的引擎。
    /// 并行泳道和嵌套容器必须使用独立实例，避免运行序号相互覆盖。
    /// </summary>
    public FlowChartExecutionEngine CreateIsolated() => new(_logContext);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Timestamp。
    /// 正文中的关键协作调用包括 GetTimestamp；具体条件和先后次序见方法体。
    /// </summary>
    private static long Timestamp() => Stopwatch.GetTimestamp();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ElapsedMs。
    /// 正文中的关键协作调用包括 GetElapsedTime；具体条件和先后次序见方法体。
    /// </summary>
    private static double ElapsedMs(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    /// <summary>
    /// 执行流程图。
    /// 支持环路：回边自动重新调度；当环内节点（如循环节点）不再激活回边时自然终止。
    /// 用户通过 CancellationToken 停止。
    /// </summary>
    /// <param name="nodes">本次要调度的节点快照。</param>
    /// <param name="connections">节点之间的连线快照。</param>
    /// <param name="ct">取消令牌。</param>
    /// <param name="log">执行日志回调。</param>
    /// <param name="onBeforeNode">可选的节点前置回调；只传递节点 ID，避免将调用方节点类型泄漏给调试协作方。</param>
    /// <param name="executionOrder">可选的实际执行顺序收集器。</param>
    /// <param name="journal">可选的执行追踪日志。</param>
    /// <param name="telemetry">可选的遥测接收器（耗时/重试/失败类型）；为空时不采集。</param>
    public async Task ExecuteAsync<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections,
        CancellationToken ct,
        Action<string> log,
        Func<string, Task>? onBeforeNode = null,
        List<TNode>? executionOrder = null,
        ExecutionJournal? journal = null,
        INodeTelemetry? telemetry = null) where TNode : INodeExecutable
    {
        if (nodes.Count == 0)
        {
            log("[引擎] 画布为空，无需执行");
            return;
        }

        // 重置本轮执行的所有节点 CurrentRunCount；RunSequence 由 Interlocked.Increment 在节点执行入口处推进
        // (重置时机：每次 ExecuteAsync 调用，即"按一次执行按钮"=一个完整执行周期；循环回边 BumpRun 不在此处重置)
        _runSeqCounter = 0;
        foreach (var node in nodes)
            node.CurrentRunCount = 0;

        // 编译期：一次性预计算邻接表、回边、前驱表、拓扑序（O(N+E)）
        var compileStart = Timestamp();
        var graph = CompiledFlowGraph<TNode>.Compile(nodes, connections);
        log($"[引擎] 图编译完成：{nodes.Count} 节点 / {connections.Count} 连线，耗时 {ElapsedMs(compileStart):F2}ms");

        // 调试模式：逐节点串行执行（注：串行模式按拓扑序遍历全图，不做分支过滤）
        if (onBeforeNode != null)
        {
            await ExecuteSequentialAsync(graph, ct, log, onBeforeNode, executionOrder, journal, telemetry);
            return;
        }

        // 有环路 → 路径追踪执行器（支持激活边终止循环）
        if (graph.HasCycle)
        {
            log("[引擎] 检测到环路，使用路径追踪模式（点击停止按钮终止）");
            await ExecuteCyclePathAsync(graph, ct, log, executionOrder, journal, telemetry);
            return;
        }

        // 无环路 → 依赖驱动异步执行（DAG 模式，支持并行）
        await ExecuteDagAsync(graph, ct, log, executionOrder, journal, telemetry);
    }

    /// <summary>
    /// 仅执行指定节点一次。使用完整画布编译图，以便从已连接的上游端口读取当前值，
    /// 但不会调度任何前驱或后继节点。
    /// </summary>
    public async Task ExecuteNodeAsync<TNode>(
        TNode node,
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections,
        CancellationToken ct,
        Action<string> log,
        ExecutionJournal? journal = null,
        INodeTelemetry? telemetry = null) where TNode : INodeExecutable
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!nodes.Any(n => n.Id == node.Id))
            throw new ArgumentException("待执行节点不属于当前执行图。", nameof(node));

        _runSeqCounter = 0;
        node.CurrentRunCount = 0;
        var graph = CompiledFlowGraph<TNode>.Compile(nodes, connections);
        log($"[单步] 开始执行选中节点 [{node.Name}]");
        await ExecuteSingleNodeAsync(
            node, graph, ct, log, orderBag: null, onExecuted: null, journal: journal, telemetry: telemetry);
        log($"[单步] 选中节点 [{node.Name}] 执行完成");
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
    /// 处理当前类型提供的操作；对应入口为 BeginNodeExecution。
    /// 正文中的关键协作调用包括 Timestamp、log、Increment；具体条件和先后次序见方法体。
    /// </summary>
    private long BeginNodeExecution<TNode>(
        TNode nodeVm,
        Action<string> log,
        ConcurrentQueue<TNode>? orderBag,
        ExecutionJournal? journal) where TNode : INodeExecutable
    {
        var startTimestamp = Timestamp();
        nodeVm.Status = NodeStatus.Running;
        orderBag?.Enqueue(nodeVm);
        log($"#{_logContext.RunId} [{nodeVm.Name}] [开始]");
        journal?.RecordNodeStart(nodeVm.Id, nodeVm.Name);
        nodeVm.Count++;
        nodeVm.CurrentRunCount++;
        nodeVm.RunSequence = Interlocked.Increment(ref _runSeqCounter);
        return startTimestamp;
    }

    /// <summary>
    /// 执行单个节点：状态切换、日志、输入填充、执行、输出同步、计时、激活端口解析，
    /// 以及超时控制 / 失败重试 / 容错策略（FailFast / ContinueOnError / RouteToErrorPort）。
    /// FailFast 策略或父级取消时抛出异常，由各调度器决定传播；
    /// ContinueOnError 与 RouteToErrorPort 不抛出，通过返回值表达失败。
    /// </summary>
    /// <param name="nodeVm">要执行的节点。</param>
    /// <param name="graph">节点所属的已编译执行图。</param>
    /// <param name="ct">用于停止节点执行的取消令牌。</param>
    /// <param name="log">接收格式化执行日志的回调。</param>
    /// <param name="orderBag">可选的实际启动顺序收集器。</param>
    /// <param name="onExecuted">可选的节点执行完成通知。</param>
    /// <param name="enforceFailFast">强制快速失败（调试串行模式使用），忽略节点的容错策略。</param>
    /// <param name="journal">可选的执行追踪日志。</param>
    /// <param name="prestartedTimestamp">节点已由调度器标记为开始时使用的时间戳。</param>
    /// <param name="telemetry">可选的遥测接收器。</param>
    private async Task<NodeRunResult> ExecuteSingleNodeAsync<TNode>(
        TNode nodeVm,
        CompiledFlowGraph<TNode> graph,
        CancellationToken ct,
        Action<string> log,
        ConcurrentQueue<TNode>? orderBag,
        Func<string>? onExecuted,
        bool enforceFailFast = false,
        ExecutionJournal? journal = null,
        long? prestartedTimestamp = null,
        INodeTelemetry? telemetry = null) where TNode : INodeExecutable
    {
        // 节点级日志统一为「#运行序号 [节点名] 内容」，与 NodeLog 面板/文件格式一致
        Action<string> nodeLog = msg => log($"#{_logContext.RunId} [{nodeVm.Name}] {msg}");
        var startTimestamp = prestartedTimestamp ??
            BeginNodeExecution(nodeVm, log, orderBag, journal);

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
                    // P2：记录每次重试（含触发重试的错误码），供 Workbench/测试观测。
                    telemetry?.RecordRetry(nodeVm.Id, attempt, RetryBoundary.GetErrorCode(failure));
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
                    // P0：每次尝试独立资源作用域；取消/失败后统一清理，避免泄漏。
                    using var resourceScope = new NodeResourceScope();
                    var effectiveCt = timeoutCts?.Token ?? ct;
                    var ctx = new NodeExecutionContext(
                        _logContext.RunId, null, nodeVm.Id, nodeVm.Name, effectiveCt,
                        new CallbackNodeLogger(nodeLog), telemetry: telemetry);
                    // 节点执行作用域：由宿主日志 adapter 按当前节点路由。
                    using (_logContext.EnterNode(nodeVm.Name))
                    {
                        // P0：优先新契约（Context），旧节点走兼容路径，零改动可运行。
                        if (nodeVm is INodeContextExecutable contextNode)
                            await contextNode.ExecuteAsync(ctx);
                        else
                            await nodeVm.ExecuteAsync(effectiveCt);
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

                // P2 重试边界：明确不可重试的分类失败（配置/视觉/取消）直接终结本轮重试，
                // 进入容错处理；可重试失败按用户配置继续。取消已在上文直接抛出。
                if (!RetryBoundary.ShouldRetry(failure, attempt, maxAttempts))
                    break;
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
                // P2：自动采集节点耗时与成功状态。
                telemetry?.RecordExecution(nodeVm.Id, nodeVm.Name, (long)elapsedOk, nodeVm.Status != NodeStatus.Error, null);

                return new NodeRunResult(ResolveActivatedPortIds(nodeVm, graph), false, false);
            }

            // ── 失败处理（已耗尽重试）──
            nodeVm.LastElapsedMs = (long)ElapsedMs(startTimestamp);
            nodeVm.Status = NodeStatus.Error;
            journal?.RecordNodeFinish(nodeVm.Id, nodeVm.Name, NodeStatus.Error, nodeVm.LastElapsedMs);
            // P0：不解析异常字符串，按稳定 ErrorCode/Category 表达失败类型。
            var normalized = FlowNodeException.FromUnknown(failure, nodeVm.Id);
            // P2：自动采集失败类型。
            telemetry?.RecordExecution(nodeVm.Id, nodeVm.Name, nodeVm.LastElapsedMs, false, normalized.ErrorCode);
            var prefix = failure is FlowChartTypeMismatchException ? "[类型错误]" : "[错误]";
            var failureLabel = normalized.Category == NodeErrorCategory.Unknown
                ? failure.Message
                : $"[{normalized.ErrorCode}/{normalized.Category}] {failure.Message}";

            switch (errorPolicy)
            {
                case NodeErrorPolicy.RouteToErrorPort:
                    var errPortId = TryRouteToErrorPort(nodeVm, graph, failureLabel);
                    if (errPortId != null)
                    {
                        log($"{prefix} {nodeVm.Name}: {failureLabel}");
                        log($"[容错] [{nodeVm.Name}] 失败已路由至 Error 端口，仅沿 Error 连线继续");
                        return new NodeRunResult(new HashSet<string> { errPortId }, true, true);
                    }
                    nodeLog("[容错] 未接线 Error 端口，按继续执行处理");
                    goto case NodeErrorPolicy.ContinueOnError;

                case NodeErrorPolicy.ContinueOnError:
                    nodeLog($"{prefix} {failureLabel}");
                    nodeLog("[容错] 策略为继续执行，下游将以上游遗留输入值继续调度");
                    return new NodeRunResult(null, true, true);

                default: // FailFast
                    nodeLog($"{prefix} {failureLabel}");
                    nodeLog($"[堆栈] {failure.StackTrace}");
                    throw failure;
            }
        }
        catch (OperationCanceledException)
        {
            nodeVm.LastElapsedMs = (long)ElapsedMs(startTimestamp);
            nodeVm.Status = NodeStatus.Error;
            // P2：取消也留下一条执行记录（E-CANCELLED），便于区分"失败"与"停止"。
            telemetry?.RecordExecution(nodeVm.Id, nodeVm.Name, nodeVm.LastElapsedMs, false, "E-CANCELLED");
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
        if (names == null)
            return null;

        // 显式上报但未指定任何端口 = 本路径在此终止。与“从未上报激活信息”区分，
        // 供条件分发的空输入等场景剪掉全部下游，无需制造可见的假端口。
        if (names.Count == 0)
            return new HashSet<string>();

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
        ExecutionJournal? journal,
        INodeTelemetry? telemetry = null) where TNode : INodeExecutable
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

        // 与硬失败分开记录：未激活的条件分支不是失败。汇聚节点只要存在至少一条
        // 已激活且成功完成的入边，就应在全部入边解析后继续执行。
        var activePredecessorCount = new ConcurrentDictionary<string, int>(
            graph.Nodes.ToDictionary(n => n.Id, _ => 0));

        // OR 汇聚可在其余前驱尚未结束时提前启动；统一用此集合保证每个 DAG 节点只投递一次。
        var scheduledNodes = new ConcurrentDictionary<string, byte>();

        var orderBag = executionOrder != null ? new ConcurrentQueue<TNode>() : null;

        var entryNodes = graph.EntryNodes.ToList();

        log($"[引擎] 入口节点 {entryNodes.Count} 个，开始依赖驱动异步执行");

        foreach (var entry in entryNodes)
        {
            QueueNodeDag(entry);
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

        /// <summary>
        /// 将可运行节点投递到线程池。仅调用 async 方法并不等于并行：若工具在首次 await 前执行
        /// 同步计算，调用线程会被占住，导致同一输出的多个分支逐个启动。
        /// </summary>
        void QueueNodeDag(TNode nodeVm)
        {
            if (!scheduledNodes.TryAdd(nodeVm.Id, 0)) return;
            _ = Task.Run(() => RunNodeDagAsync(nodeVm));
        }

        /// <summary>DAG 模式单节点任务：执行核心 + 异常策略收集 + 下游调度。</summary>
        async Task RunNodeDagAsync(TNode nodeVm)
        {
            IReadOnlySet<string>? activated = null;
            bool propagateFailure = false;
            NodeRunResult? result = null;
            try
            {
                result = await ExecuteSingleNodeAsync(nodeVm, graph, linkedCts.Token, log, orderBag,
                    onExecuted: () =>
                    {
                        // 节点本地 Count/CurrentRunCount 已在 ExecuteSingleNodeAsync 入口处累加，此处只推进全局执行进度
                        var executed = Interlocked.Increment(ref counter.Value);
                        return $" ({executed}/{totalCount})";
                    },
                    journal: journal,
                    telemetry: telemetry);
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
                        ScheduleSuccessorDag(conn, propagateFailure, result);
                    }
                }
            }
        }

        /// <summary>
        /// DAG 下游调度：分别记录硬失败、活动边与未激活边。
        /// 全部静态入边都解析后：有硬失败则跳过；否则至少有一条活动边就执行；
        /// 所有入边均未激活才剪枝。这样条件分支可以安全汇聚，而并行分支仍会等待全部完成。
        /// </summary>
        void ScheduleSuccessorDag(ConnectionModel conn, bool propagateFailure, NodeRunResult? result)
        {
            var succId = conn.TargetNodeId;

            var activeEdge = false;
            if (propagateFailure)
            {
                // FailFast 失败：下游全部标记待跳过
                failedPredecessorCount.AddOrUpdate(succId, 1, (_, v) => v + 1);
            }
            else if (result?.ActivatedIds != null &&
                     graph.NodeMap[conn.SourceNodeId].FindPortById(conn.SourcePortId)?.DataType == PortType.Flow &&
                     !result.ActivatedIds.Contains(conn.SourcePortId))
            {
                // 激活选择只过滤 Flow 控制流边。数据边始终完成传值/依赖解析，不能因为
                // 同一源节点本轮选择了另一个 Flow 端口就被误当作未激活分支。
                log($"[分支] [{graph.NodeMap[conn.SourceNodeId].Name}] 未激活端口连线 → [{graph.NodeMap[succId].Name}]");
            }
            else
            {
                // 有 Flow 入边时，只有 Flow 连线携带执行令牌；数据连线仅用于等待与传值，
                // 不能越过未命中的条件分支激活节点。若目标完全没有 Flow 入边，则保留旧图中
                // “纯数据连线驱动后继”的兼容行为（FlowInstance/子流程历史文档依赖此语义）。
                var targetPort = graph.NodeMap[succId].FindPortById(conn.TargetPortId);
                if (targetPort?.DataType == PortType.Flow || !graph.NodesWithFlowInput.Contains(succId))
                {
                    activePredecessorCount.AddOrUpdate(succId, 1, (_, v) => v + 1);
                    activeEdge = true;
                }
            }

            var remaining = remainingPredecessors.AddOrUpdate(succId, _ => 0, (_, c) => c - 1);
            if (!graph.NodeMap.TryGetValue(succId, out var succNode)) return;

            // AND 汇聚收到第一条活动分支后立即显示“运行中/等待”，但只有全部输入
            // 都到达后才真正执行并变为完成。这样 UI 能准确表达汇聚正在等待另一支路。
            if (activeEdge && remaining > 0 &&
                succNode.FlowJoinMode == FlowJoinMode.AllActive &&
                succNode.Status == NodeStatus.Idle)
            {
                succNode.Status = NodeStatus.Running;
                log($"[汇聚] [{succNode.Name}] 已收到分支，等待其余输入");
            }

            if (activeEdge && succNode.FlowJoinMode == FlowJoinMode.AnyActive)
            {
                QueueNodeDag(succNode);
            }

            if (remaining != 0 || scheduledNodes.ContainsKey(succId)) return;

            if (failedPredecessorCount.GetValueOrDefault(succId, 0) > 0)
            {
                SkipDownstream(succNode, propagateFailure: true);
            }
            else if (ShouldRunAfterAllInputsResolved(succNode, graph, activePredecessorCount.GetValueOrDefault(succId, 0)))
            {
                QueueNodeDag(succNode);
            }
            else
            {
                SkipDownstream(succNode, propagateFailure: false);
            }
        }

        /// <summary>
        /// 递归解析被跳过节点的出边。硬失败继续传播失败；未激活分支只传播“无令牌”。
        /// 被跳过的节点不执行，状态保持 Idle。
        /// </summary>
        void SkipDownstream(TNode nodeVm, bool propagateFailure)
        {
            log($"[跳过] {nodeVm.Name} - 上游未执行（失败或分支未选中）");
            journal?.RecordNodeSkipped(nodeVm.Id, nodeVm.Name);
            if (nodeVm.Status == NodeStatus.Running)
                nodeVm.Status = NodeStatus.Idle;

            // 解析完成信号
            completionSources[nodeVm.Id].TrySetResult();

            // 递归处理后继
            if (graph.Successors.TryGetValue(nodeVm.Id, out var succList))
            {
                foreach (var succId in succList)
                {
                    if (propagateFailure)
                        failedPredecessorCount.AddOrUpdate(succId, 1, (_, value) => value + 1);

                    var remaining = remainingPredecessors.AddOrUpdate(
                        succId, _ => 0, (_, current) => current - 1);

                    if (remaining == 0)
                    {
                        if (graph.NodeMap.TryGetValue(succId, out var succNode))
                        {
                            if (scheduledNodes.ContainsKey(succId))
                                continue;
                            if (failedPredecessorCount.GetValueOrDefault(succId, 0) > 0)
                            {
                                SkipDownstream(succNode, propagateFailure: true);
                            }
                            else if (ShouldRunAfterAllInputsResolved(
                                         succNode, graph, activePredecessorCount.GetValueOrDefault(succId, 0)))
                            {
                                QueueNodeDag(succNode);
                            }
                            else
                            {
                                SkipDownstream(succNode, propagateFailure: false);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ShouldRunAfterAllInputsResolved。
    /// 正文中的关键协作调用包括 Count、FindPortById；具体条件和先后次序见方法体。
    /// </summary>
    private static bool ShouldRunAfterAllInputsResolved<TNode>(
        TNode node,
        CompiledFlowGraph<TNode> graph,
        int activeCount) where TNode : INodeExecutable
    {
        if (activeCount == 0) return false;
        if (node.FlowJoinMode != FlowJoinMode.AllActive) return true;

        var requiredFlowInputs = graph.IncomingConnections[node.Id].Count(connection =>
            node.FindPortById(connection.TargetPortId)?.DataType == PortType.Flow);
        return requiredFlowInputs > 0 && activeCount >= requiredFlowInputs;
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
        ExecutionJournal? journal,
        INodeTelemetry? telemetry = null) where TNode : INodeExecutable
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

        var cycleSequence = 0;

        // 队列级回边目标去重：循环体存在多条并行末端时，归一化器会为每个末端各合成一条回边，
        // 若不去重则循环头一轮被入队多次 → 计数翻倍、Completed 后残留调度复活循环（无限活锁）。
        // 该集合跨节点持久（区别于旧实现的单次执行局部去重），目标出队执行时移除。
        var pendingBackEdgeTargets = new HashSet<string>();

        // 环路模式也必须遵守 OR 汇聚：任一活动输入到达即可调度。
        // 目标出队前保持占位，避免同一批同时到达的输入重复投递同一个汇聚节点。
        var pendingAnyJoinTargets = new HashSet<string>();
        // AND 汇聚首个令牌到达即进入 Running，保留开始时刻以计算完整等待耗时。
        var pendingAllJoinStarts = new Dictionary<string, long>();
        var runningNodeIds = new HashSet<string>();
        var running = new List<(
            string Id,
            TNode Node,
            Task<NodeRunResult> Task)>();
        using var cycleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var runToken = cycleCts.Token;

        while (queue.Count > 0 || running.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            // 立即启动当前所有 ready 节点；已在运行的同一节点不可重入。
            var waveSize = queue.Count;
            for (var index = 0; index < waveSize; index++)
            {
                var candidateId = queue.Dequeue();
                pendingAnyJoinTargets.Remove(candidateId);
                if (!nodeMap.TryGetValue(candidateId, out var candidateNode))
                {
                    pendingBackEdgeTargets.Remove(candidateId);
                    continue;
                }

                if (remaining.GetValueOrDefault(candidateId, 0) > 0 || runningNodeIds.Contains(candidateId))
                {
                    queue.Enqueue(candidateId);
                    continue;
                }

                pendingBackEdgeTargets.Remove(candidateId);
                var prestarted = pendingAllJoinStarts.Remove(candidateId, out var startedAt)
                    ? startedAt
                    : (long?)null;
                if (prestarted == null)
                    executionOrder?.Add(candidateNode);
                runningNodeIds.Add(candidateId);
                var task = Task.Run(() =>
                ExecuteSingleNodeAsync(candidateNode, graph, runToken, log, orderBag: null,
                    onExecuted: () =>
                    {
                        var sequence = Interlocked.Increment(ref cycleSequence);
                        return $" (#{sequence})";
                    },
                    journal: journal,
                    prestartedTimestamp: prestarted,
                    telemetry: telemetry), runToken);
                running.Add((candidateId, candidateNode, task));
            }

            // 没有可执行节点，只剩未收齐的 AND 汇聚：本次路径自然结束。
            if (running.Count == 0)
                break;

            // 任意一个节点完成就立即传播后继，不等待同批的最长分支。
            var completedTask = await Task.WhenAny(running.Select(item => item.Task));
            var completedIndex = running.FindIndex(item => ReferenceEquals(item.Task, completedTask));
            var completed = running[completedIndex];
            NodeRunResult result;
            try
            {
                result = await completed.Task;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
            catch
            {
                cycleCts.Cancel();
                try { await Task.WhenAll(running.Select(item => item.Task)); }
                catch { /* 保留首个失败作为对外异常 */ }
                throw;
            }
            running.RemoveAt(completedIndex);
            runningNodeIds.Remove(completed.Id);

            var nodeId = completed.Id;
            var nodeVm = completed.Node;
            var activated = result.ActivatedIds;

            // 完成事件在调度线程上串行传播，保证 remaining/回边集合无竞态。
            foreach (var conn in graph.SuccessorConnections[nodeId])
            {
                var succId = conn.TargetNodeId;

                var sourcePort = nodeVm.FindPortById(conn.SourcePortId);
                if (activated != null && sourcePort?.DataType == PortType.Flow &&
                    !activated.Contains(conn.SourcePortId))
                {
                    log($"[分支] [{nodeVm.Name}] 未激活该端口连线，剪枝 → [{nodeMap[succId].Name}]");
                    continue;
                }

                if (graph.BackEdges.Contains((conn.SourceNodeId, succId)))
                {
                    if (!pendingBackEdgeTargets.Add(succId)) continue;

                    _logContext.BumpRun();
                    log($"[环路] 重新调度 {nodeMap[succId].Name}");

                    if (nodeMap[succId].FlowJoinMode != FlowJoinMode.AllActive)
                    {
                        remaining[succId] = 0;
                        nodeMap[succId].Status = NodeStatus.Idle;
                        queue.Enqueue(succId);
                        continue;
                    }
                    // AND 汇聚的回边也只是其中一个输入令牌，不能绕过其他输入直接执行。
                }

                if (nodeMap[succId].FlowJoinMode == FlowJoinMode.AnyActive)
                {
                    if (pendingAnyJoinTargets.Add(succId))
                    {
                        remaining[succId] = 0;
                        queue.Enqueue(succId);
                    }
                    continue;
                }

                var currentRemaining = remaining.GetValueOrDefault(
                    succId, graph.Predecessors[succId].Count);
                if (currentRemaining <= 0)
                    currentRemaining = graph.Predecessors[succId].Count;
                var rem = currentRemaining - 1;
                remaining[succId] = rem;
                if (rem <= 0)
                {
                    remaining[succId] = 0;
                    queue.Enqueue(succId);
                }
                else if (nodeMap[succId].FlowJoinMode == FlowJoinMode.AllActive &&
                         !pendingAllJoinStarts.ContainsKey(succId))
                {
                    executionOrder?.Add(nodeMap[succId]);
                    pendingAllJoinStarts[succId] = BeginNodeExecution(
                        nodeMap[succId], log, orderBag: null, journal);
                }
            }
        }

        // 条件分支剪枝后可能有 AND 汇聚永远收不齐；流程自然结束时恢复 Idle。
        foreach (var nodeId in pendingAllJoinStarts.Keys)
            if (nodeMap[nodeId].Status == NodeStatus.Running)
                nodeMap[nodeId].Status = NodeStatus.Idle;

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
        Func<string, Task> onBeforeNode,
        List<TNode>? executionOrder,
        ExecutionJournal? journal,
        INodeTelemetry? telemetry = null) where TNode : INodeExecutable
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
                await onBeforeNode(nodeVm.Id);
                executionOrder?.Add(nodeVm);

                // 调试模式强制 FailFast：失败立即中断步进（重试/超时仍然生效）
                await ExecuteSingleNodeAsync(nodeVm, graph, ct, log, orderBag: null,
                    // 调试模式：节点 Count/CurrentRunCount 已在 ExecuteSingleNodeAsync 入口处累加
                    onExecuted: () => "",
                    enforceFailFast: true,
                    journal: journal,
                    telemetry: telemetry);
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
            if (PortValueTransfer.TryTransfer(
                    sourceNode, conn.SourcePortId,
                    targetNode, conn.TargetPortId,
                    out var transferredValue))
            {
                // Journal 属于执行引擎编排职责，不进入端口值传递模块。
                journal?.RecordEdgeTransfer(conn.Id, transferredValue);
            }
        }
    }

    #endregion
}
