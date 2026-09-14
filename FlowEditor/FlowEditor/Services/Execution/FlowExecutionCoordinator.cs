using System.Collections.ObjectModel;
using Extension.Struct;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;

namespace FlowEditor.Services.Execution;

/// <summary>
/// 协调单个画布的完整执行会话，集中管理校验、作用域准备、取消、调试、恢复、
/// 循环运行、日志路由和状态收尾。调用方不需要了解执行会话的生命周期约束。
/// </summary>
internal sealed class FlowExecutionCoordinator
{
    private readonly FlowChartModel _model;
    private readonly ObservableCollection<NodeViewModel> _nodes;
    private readonly ObservableCollection<ConnectionModel> _connections;
    private readonly FlowChartExecutionEngine _engine;
    private readonly FlowDocumentMapper _documentMapper;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly FlowOutputBuffer _output;
    private readonly Func<string?> _processName;
    private readonly List<NodeViewModel> _executionOrder = [];
    private CancellationTokenSource? _executionCts;

    /// <summary>创建绑定到一个画布会话的执行协调模块。</summary>
    public FlowExecutionCoordinator(
        FlowChartModel model,
        ObservableCollection<NodeViewModel> nodes,
        ObservableCollection<ConnectionModel> connections,
        FlowChartExecutionEngine engine,
        FlowDocumentMapper documentMapper,
        IUiDispatcher uiDispatcher,
        FlowOutputBuffer output,
        Func<string?> processName)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _processName = processName ?? throw new ArgumentNullException(nameof(processName));
    }

    /// <summary>当前执行节点索引。</summary>
    public int CurrentExecutionIndex { get; private set; }

    /// <summary>调试断点和单步门闩。</summary>
    public DebugController Debug { get; } = new();

    /// <summary>节点与连线执行追踪。</summary>
    public ExecutionJournal Journal { get; } = new();

    private string FlowLogName => _model.TabTitle;

    /// <summary>执行当前画布一次。</summary>
    public async Task RunAsync(Action<string> logCallback, bool debugMode)
    {
        if (_model.IsRunning || !RunPreValidation(logCallback)) return;

        _model.IsRunning = true;
        _model.IsDebugMode = debugMode;
        Debug.StepMode = debugMode;
        _model.StatusText = "正在执行...";
        _executionCts = new CancellationTokenSource();
        _executionOrder.Clear();
        CurrentExecutionIndex = 0;
        long flowRunId = 0;

        try
        {
            foreach (var node in _nodes)
                node.ResetStatus();
            PrepareExecutionScopeRuntime();
            _output.Clear();

            using (FlowLogContext.EnterProcess(_processName() ?? FlowLogName))
            using (FlowLogContext.EnterFlow(FlowLogName))
            using (ConsoleOutputRouter.EnterScope(message => DispatchLog(logCallback, message)))
            {
                void Panel(string message, long runId) => PanelLog(message, logCallback, runId);

                flowRunId = FlowLogContext.RunId;
                Panel("=== 开始执行流程 ===", flowRunId);
                var graph = CreateTopLevelGraph();
                await _engine.ExecuteAsync(
                    graph.Nodes,
                    graph.Connections,
                    _executionCts.Token,
                    message => Panel(message, flowRunId),
                    _model.IsDebugMode ? OnBeforeNodeExecute : null,
                    executionOrder: null,
                    journal: Journal);
                Panel("=== 执行完成 ===", flowRunId);
            }

            _model.StatusText = "执行完成";
        }
        catch (OperationCanceledException)
        {
            PanelLog("=== 已取消执行 ===", logCallback, flowRunId);
            _model.StatusText = "已取消";
        }
        catch (Exception ex)
        {
            PanelLog($"[错误] {ex.GetType().Name}: {ex.Message}", logCallback, flowRunId);
            FlowLogContext.Write($"流程执行异常 {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            _model.StatusText = "执行出错";
        }
        finally
        {
            FinishExecutionSession(resetDebug: true);
        }
    }

    /// <summary>释放当前断点或单步暂停。</summary>
    public void StepContinue()
    {
        Debug.Continue();
        _model.IsPaused = false;
    }

    /// <summary>只执行指定节点一次，不调度其后继。</summary>
    public async Task StepContinueAsync(NodeViewModel node, Action<string> logCallback)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.Contains(node))
            throw new ArgumentException("选中节点不属于当前画布。", nameof(node));
        if (node.IsExcludedFromExecution)
        {
            _model.StatusText = "不执行容器中的节点不能单步执行";
            logCallback($"[单步] [{node.Name}] 位于不执行节点容器，已跳过");
            return;
        }
        if (_model.IsRunning)
        {
            _model.StatusText = "流程运行中，不能单独执行节点";
            return;
        }

        _model.IsRunning = true;
        _model.StatusText = $"正在单步执行节点 [{node.Name}]...";
        _executionCts = new CancellationTokenSource();
        node.ResetStatus();
        try
        {
            PrepareExecutionScopeRuntime();
            var graph = ExecutionGraphScope.Create(
                _nodes.Select(item => item.Model),
                _connections,
                node.Model.ParentExecutionContainerId,
                node.Model.ExecutionLaneId);
            await _engine.ExecuteNodeAsync(
                node.Model, graph.Nodes, graph.Connections,
                _executionCts.Token, logCallback, Journal);
            _model.StatusText = $"节点 [{node.Name}] 单步执行完成";
        }
        catch (OperationCanceledException)
        {
            _model.StatusText = $"节点 [{node.Name}] 单步执行已取消";
        }
        catch (Exception ex)
        {
            logCallback($"[单步][错误] [{node.Name}] {ex.Message}");
            _model.StatusText = $"节点 [{node.Name}] 单步执行失败";
        }
        finally
        {
            FinishExecutionSession(resetDebug: false);
        }
    }

    /// <summary>从指定节点恢复或跳转，并继续当前作用域的可达路径。</summary>
    public async Task ContinueFromNodeAsync(NodeViewModel node, Action<string> logCallback)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(logCallback);
        if (!_nodes.Contains(node))
            throw new ArgumentException("选中节点不属于当前画布。", nameof(node));
        if (node.IsExcludedFromExecution) return;

        if (_model.IsRunning)
        {
            await ResumeRunningParallelLaneAsync(node, logCallback);
            return;
        }

        _model.IsRunning = true;
        _model.StatusText = $"正在从节点 [{node.Name}] 恢复执行...";
        _executionCts = new CancellationTokenSource();
        try
        {
            PrepareExecutionScopeRuntime();
            var token = _executionCts.Token;
            var parentId = node.Model.ParentExecutionContainerId;
            if (parentId == null)
            {
                var top = CreateTopLevelGraph();
                var recovery = ExecutionGraphScope.CreateReachable(top.Nodes, top.Connections, node.Id);
                await _engine.ExecuteAsync(
                    recovery.Nodes, recovery.Connections, token, logCallback, journal: Journal);
            }
            else
            {
                var parent = FindNode(parentId)?.Model
                    ?? throw new InvalidOperationException($"找不到节点 [{node.Name}] 的执行容器。");
                var parentWasFailed = parent.Status == NodeStatus.Error;
                await ResumeContainerAsync(parent, node.Id, token, logCallback);
                parent.Status = NodeStatus.Success;
                if (parentWasFailed)
                    await ExecuteTopLevelAfterAsync(parent.Id, token, logCallback);
            }

            _model.StatusText = $"从节点 [{node.Name}] 恢复执行完成";
        }
        catch (OperationCanceledException)
        {
            _model.StatusText = "恢复执行已取消";
        }
        catch (Exception ex)
        {
            logCallback($"[恢复][错误] 从节点 [{node.Name}] 执行失败：{ex.Message}");
            _model.StatusText = "恢复执行出错";
        }
        finally
        {
            FinishExecutionSession(resetDebug: false);
        }
    }

    /// <summary>请求停止当前执行会话。</summary>
    public void Stop()
    {
        if (!_model.IsRunning) return;
        _executionCts?.Cancel();
        _model.IsRunning = false;
        _model.IsPaused = false;
        _model.IsLoopRunning = false;
        _model.StatusText = "正在停止...";
        _output.Flush();
    }

    /// <summary>无限循环执行画布，直到收到停止请求。</summary>
    public async Task RunLoopAsync(Action<string> logCallback)
    {
        if (_model.IsRunning || _model.IsLoopRunning || !RunPreValidation(logCallback)) return;

        _model.IsLoopRunning = true;
        _model.IsRunning = true;
        _model.StatusText = "正在循环执行...";
        _model.CurrentLoopIndex = 0;
        _executionCts = new CancellationTokenSource();
        long loopRunId = 0;

        try
        {
            _output.Clear();
            logCallback("=== 开始循环执行 ===");
            logCallback($"循环模式：间隔 {_model.LoopInterval}ms");
            var iteration = 0;
            using (ConsoleOutputRouter.EnterScope(message => DispatchLog(logCallback, message)))
            {
                while (true)
                {
                    _executionCts.Token.ThrowIfCancellationRequested();
                    iteration++;
                    _model.CurrentLoopIndex = iteration;
                    logCallback($"--- 第 {iteration} 次循环开始 ---");
                    _model.StatusText = $"循环执行中... 第 {iteration} 次";
                    foreach (var node in _nodes)
                        node.ResetStatus();
                    PrepareExecutionScopeRuntime();
                    _executionOrder.Clear();
                    CurrentExecutionIndex = 0;

                    using (FlowLogContext.EnterProcess(_processName() ?? FlowLogName))
                    using (FlowLogContext.EnterFlow(FlowLogName))
                    {
                        void Panel(string message) => PanelLog(message, logCallback);
                        loopRunId = FlowLogContext.RunId;
                        var graph = CreateTopLevelGraph();
                        await _engine.ExecuteAsync(
                            graph.Nodes, graph.Connections, _executionCts.Token, Panel,
                            _model.IsDebugMode ? OnBeforeNodeExecute : null,
                            executionOrder: null,
                            journal: Journal);
                    }

                    logCallback($"--- 第 {iteration} 次循环完成 ---");
                    if (_model.LoopInterval > 0)
                    {
                        logCallback($"[等待 {_model.LoopInterval}ms 后开始下一次循环...]");
                        await Task.Delay(_model.LoopInterval, _executionCts.Token);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            PanelLog("=== 已停止循环执行 ===", logCallback, loopRunId);
            _model.StatusText = "循环执行已停止";
        }
        catch (Exception ex)
        {
            PanelLog($"[错误] {ex.Message}", logCallback, loopRunId);
            _model.StatusText = "循环执行出错";
        }
        finally
        {
            FinishExecutionSession(resetDebug: true);
            _model.IsLoopRunning = false;
        }
    }

    /// <summary>请求停止循环执行。</summary>
    public void StopLoop()
    {
        if (!_model.IsLoopRunning) return;
        _executionCts?.Cancel();
        _model.IsLoopRunning = false;
        _model.IsRunning = false;
        _model.IsPaused = false;
        _model.StatusText = "正在停止循环执行...";
        _output.Flush();
    }

    /// <summary>将当前编辑图编译为可创建隔离运行实例的不可变程序。</summary>
    public FlowProgram CompileProgram()
    {
        var (activeNodes, activeConnections) = CreateActiveGraph();
        return FlowProgram.FromDocument(_documentMapper.ToDocument(
            _model.TabTitle,
            activeNodes.Select(node => node.Model),
            activeConnections,
            _model));
    }

    /// <summary>处理调试执行前暂停，并同步当前执行索引。</summary>
    private async Task OnBeforeNodeExecute(string nodeId)
    {
        var node = FindNode(nodeId)
            ?? throw new InvalidOperationException($"执行引擎回调了当前画布不存在的节点: {nodeId}");
        _executionOrder.Add(node);
        CurrentExecutionIndex = _executionOrder.Count - 1;
        _model.IsPaused = true;
        try
        {
            await Debug.WaitIfPausedAsync(nodeId, _executionCts?.Token ?? CancellationToken.None);
        }
        finally
        {
            _model.IsPaused = false;
        }
    }

    /// <summary>恢复仍在运行的并行容器中的独立泳道。</summary>
    private async Task ResumeRunningParallelLaneAsync(NodeViewModel node, Action<string> logCallback)
    {
        var parent = node.Model.ParentExecutionContainerId == null
            ? null
            : FindNode(node.Model.ParentExecutionContainerId)?.Model as ParallelContainerNode;
        if (parent == null || _executionCts == null) return;

        try
        {
            await parent.ResumeFromNodeAsync(node.Id, _executionCts.Token, logCallback);
        }
        catch (OperationCanceledException) when (_executionCts.IsCancellationRequested)
        {
            logCallback($"[恢复] 并行分支 [{node.Name}] 已取消");
        }
        catch (Exception ex)
        {
            logCallback($"[恢复][错误] 并行分支 [{node.Name}] 恢复失败：{ex.Message}");
        }
    }

    /// <summary>按容器类型恢复其内部作用域。</summary>
    private static Task ResumeContainerAsync(
        NodeModel parent,
        string startNodeId,
        CancellationToken ct,
        Action<string> logCallback) => parent switch
        {
            ParallelContainerNode parallel => parallel.ResumeFromNodeAsync(startNodeId, ct, logCallback),
            LoopContainerNode loop => loop.ResumeFromNodeAsync(startNodeId, ct, logCallback),
            _ => throw new InvalidOperationException($"容器 [{parent.Name}] 不支持恢复执行。")
        };

    /// <summary>从已完成顶层复合节点之后继续执行，不重复执行容器本身。</summary>
    private async Task ExecuteTopLevelAfterAsync(
        string completedNodeId,
        CancellationToken ct,
        Action<string> logCallback)
    {
        var top = CreateTopLevelGraph();
        var withCompleted = ExecutionGraphScope.CreateReachable(
            top.Nodes, top.Connections, completedNodeId);
        var remainingNodes = withCompleted.Nodes.Where(node => node.Id != completedNodeId).ToList();
        if (remainingNodes.Count == 0) return;
        var remainingIds = remainingNodes.Select(node => node.Id).ToHashSet();
        var remainingConnections = withCompleted.Connections.Where(connection =>
                remainingIds.Contains(connection.SourceNodeId) &&
                remainingIds.Contains(connection.TargetNodeId))
            .ToList();
        await _engine.ExecuteAsync(
            remainingNodes, remainingConnections, ct, logCallback, journal: Journal);
    }

    /// <summary>执行静态校验并发布全部诊断。</summary>
    private bool RunPreValidation(Action<string> logCallback)
    {
        var (activeNodes, activeConnections) = CreateActiveGraph();
        var report = FlowValidator.Analyze(
            activeNodes.Select(node => node.Model).ToList(), activeConnections);
        foreach (var diagnostic in report.Diagnostics)
        {
            var tag = diagnostic.Severity switch
            {
                FlowDiagnosticSeverity.Error => "错误",
                FlowDiagnosticSeverity.Warning => "警告",
                _ => "提示"
            };
            logCallback($"[校验][{tag}] {diagnostic.Message}");
        }

        if (!report.HasErrors) return true;
        _model.StatusText = $"校验失败：{report.ErrorCount} 个错误";
        logCallback($"=== 校验失败，共 {report.ErrorCount} 个错误 ===");
        _output.Flush();
        return false;
    }

    /// <summary>建立结构化容器执行时使用的完整活动图上下文。</summary>
    private void PrepareExecutionScopeRuntime()
    {
        var (activeNodes, activeConnections) = CreateActiveGraph();
        var models = activeNodes.Select(node => node.Model).ToList();
        var runtime = new ExecutionScopeRuntime(models, activeConnections, _engine);
        foreach (var node in models)
            node.ExecutionScopeRuntime = runtime;
    }

    /// <summary>筛选当前画布的活动节点与有效连线。</summary>
    private (List<NodeViewModel> Nodes, List<ConnectionModel> Connections) CreateActiveGraph()
    {
        var activeNodes = _nodes.Where(node => !node.IsExcludedFromExecution).ToList();
        var activeIds = activeNodes.Select(node => node.Id).ToHashSet();
        var activeConnections = _connections.Where(connection =>
                activeIds.Contains(connection.SourceNodeId) &&
                activeIds.Contains(connection.TargetNodeId))
            .ToList();
        return (activeNodes, activeConnections);
    }

    /// <summary>创建调度器可见的顶层复合图。</summary>
    private ExecutionGraphScope<NodeModel> CreateTopLevelGraph() =>
        ExecutionGraphScope.CreateTopLevel(_nodes.Select(node => node.Model), _connections);

    /// <summary>统一发布面板和文件日志，并过滤低价值剪枝消息。</summary>
    private void PanelLog(string message, Action<string> logCallback, long runId = 0)
    {
        if (message.StartsWith("[跳过]") || message.StartsWith("[分支]")) return;
        var id = runId > 0 ? runId : FlowLogContext.RunId;
        if (!message.StartsWith("#") && id > 0)
            message = $"#{id} {message}";
        DispatchLog(logCallback, message);

        var level = message.Contains("[错误]") || message.Contains("[类型错误]") || message.Contains("[堆栈]")
            ? ELogLevel.Error
            : message.Contains("[容错]") || message.Contains("[重试]")
                ? ELogLevel.Warning
                : ELogLevel.Information;
        FlowLogContext.Write(message, level);
    }

    /// <summary>通过宿主线程 adapter 发布执行日志。</summary>
    private void DispatchLog(Action<string> logCallback, string message)
        => _uiDispatcher.Invoke(() => logCallback(message));

    /// <summary>查找当前画布节点。</summary>
    private NodeViewModel? FindNode(string nodeId) =>
        _nodes.FirstOrDefault(node => node.Id == nodeId);

    /// <summary>释放执行资源并恢复公共运行状态。</summary>
    private void FinishExecutionSession(bool resetDebug)
    {
        _output.Flush();
        _model.IsRunning = false;
        _model.IsDebugMode = false;
        _model.IsPaused = false;
        if (resetDebug)
        {
            Debug.StepMode = false;
            Debug.Continue();
        }
        _executionCts?.Dispose();
        _executionCts = null;
    }
}
