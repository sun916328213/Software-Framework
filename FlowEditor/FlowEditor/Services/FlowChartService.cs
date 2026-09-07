using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Extension.Struct;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Prism.Ioc;

namespace FlowEditor.Services;

/// <summary>
/// 流程图服务。
/// 声明并持有 FlowChartModel，提供节点/连线集合访问及核心业务方法。
/// 依赖通过构造器注入（IoC），不直接创建依赖对象，便于测试与解耦。
/// </summary>
public class FlowChartService : IFlowChartService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>流程图运行时模型。ViewModel 和 View 直接绑定到此模型属性。</summary>
    public FlowChartModel Model { get; } = new();

    /// <summary>画布上所有节点。</summary>
    public ObservableCollection<NodeViewModel> Nodes { get; } = new();

    /// <summary>所有连线。</summary>
    public ObservableCollection<ConnectionModel> Connections { get; } = new();

    /// <summary>撤销/重做服务。</summary>
    public IUndoRedoService UndoRedo { get; }

    private readonly FlowChartExecutionEngine _engine;
    private readonly IFlowChartPersistenceService _persistence;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _executionCts;
    private readonly List<NodeViewModel> _executionOrder = new();

    // ── 日志缓冲（P0 性能优化）──
    // OutputText 直接绑定到 UI，逐行字符串拼接会导致 O(n²) 拷贝且长循环运行时无限膨胀。
    // 改为 StringBuilder 缓冲 + 超长截断 + 节流推送（默认 100ms），执行结束时 FlushLog 保证完整。

    /// <summary>日志缓冲锁（AppendLog 可能从多线程调用）。</summary>
    private readonly object _logGate = new();
    private readonly StringBuilder _logBuilder = new();
    private long _lastLogFlushTimestamp;

    /// <summary>日志缓冲最大字符数，超过后丢弃较早的一半。</summary>
    private const int MaxLogChars = 200_000;

    /// <summary>输出面板最小刷新间隔（毫秒）。</summary>
    private const int LogFlushIntervalMs = 100;

    /// <summary>容器提供器（用于在 ToolBlock 内部编辑器创建新的 ViewModel 实例）。</summary>
    public IContainerProvider? ContainerProvider { get; }

    public FlowChartService(
        IFlowChartPersistenceService persistence,
        FlowChartExecutionEngine engine,
        IDialogService dialogService,
        IContainerProvider? containerProvider = null)
    {
        _persistence = persistence;
        _engine = engine;
        _dialogService = dialogService;
        ContainerProvider = containerProvider;
        UndoRedo = new UndoRedoService();
    }

    /// <summary>当前执行节点索引（用于单步调试）。</summary>
    public int CurrentExecutionIndex { get; private set; }

    /// <summary>流程名（宿主设置，如 AutoFlow / InitializeFlow）；为节点日志的上级目录。</summary>
    public string? ProcessName { get; set; }

    /// <summary>任务日志名称：取标签页名（AddNewTab/流程文件已保证唯一，如 流程图1），
    /// 日志路径为 D:\DeviceData\Log\Flow\{流程名}\{任务名}\yyyyMMdd.txt。</summary>
    private string FlowLogName => Model.TabTitle;

    /// <summary>调试控制器（P4）：断点与单步的 TCS 门闩。</summary>
    public DebugController Debug { get; } = new();

    /// <summary>画布执行的日志/数据追踪器（P4）：记录节点事件与连线传值。</summary>
    public ExecutionJournal Journal { get; } = new();

    // ── 执行流程 ──

    /// <summary>执行流程图。</summary>
    public async Task RunAsync(Action<string> logCallback, bool debugMode = false)
    {
        if (Model.IsRunning) return;

        // 前置校验：静态分析一次性报告（IN 连线 / 类型兼容 / 孤立节点 / 绑定有效性）
        if (!RunPreValidation(logCallback))
            return;

        Model.IsRunning = true;
        Model.IsDebugMode = debugMode;
        Debug.StepMode = debugMode;
        _executionCts = new CancellationTokenSource();
        _executionOrder.Clear();
        CurrentExecutionIndex = 0;

        // 捕获本次运行序号，供流程作用域外（取消/异常）的收尾消息补 #前缀
        long flowRunId = 0;

        try
        {
            // 重置所有节点状态
            foreach (var n in Nodes)
                n.ResetStatus();

            ClearLog();

            // 进入流程（容器级）与任务作用域：节点日志按「流程名\任务名」路由落盘
            using (FlowLogContext.EnterProcess(ProcessName ?? FlowLogName))
            using (FlowLogContext.EnterFlow(FlowLogName))
            using (ConsoleOutputRouter.EnterScope(msg => Application.Current?.Dispatcher.Invoke(() => logCallback(msg))))
            {
                // 面板输出（引擎执行轨迹与运行标记）同步落盘到流程日志文件：
                // NodeLog/节点 Console 已有独立落盘通道，此处只覆盖引擎 log 轨迹，避免重复写入。
                // runId 传本次运行根 RunId：让 [引擎] / === 执行完成 === 等无节点消息使用"运行级"编号，
                // 不被 BumpRun 推进的"轮次 RunId"覆盖；节点级日志仍走 RunId 区分循环各轮。
                void Panel(string msg, long runId) => PanelLog(msg, logCallback, runId);

                flowRunId = FlowLogContext.RunId;
                Panel("=== 开始执行流程 ===", flowRunId);
                await _engine.ExecuteAsync(
                    Nodes.ToList(),
                    Connections.ToList(),
                    _executionCts.Token,
                    msg => Panel(msg, flowRunId),
                    Model.IsDebugMode ? OnBeforeNodeExecute : null,
                    _executionOrder,
                    Journal);
                Panel("=== 执行完成 ===", flowRunId);
            }

            Model.StatusText = "执行完成";
        }
        catch (OperationCanceledException)
        {
            PanelLog("=== 已取消执行 ===", logCallback, flowRunId);
            Model.StatusText = "已取消";
        }
        catch (Exception ex)
        {
            // 记录完整异常信息（含堆栈）便于排障；面板仅显示消息摘要，堆栈落盘到日志文件
            PanelLog($"[错误] {ex.GetType().Name}: {ex.Message}", logCallback, flowRunId);
            FlowLogContext.Write($"流程执行异常 {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            Model.StatusText = "执行出错";
        }
        finally
        {
            FlushLog();
            Model.IsRunning = false;
            Model.IsDebugMode = false;
            Model.IsPaused = false;
            Debug.StepMode = false;
            Debug.Continue(); // 释放可能残留的调试门闩
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    /// <summary>单步执行回调：命中断点或单步模式时经 TCS 门闩挂起（P4，替代轮询）。</summary>
    private async Task OnBeforeNodeExecute(NodeViewModel node)
    {
        _executionOrder.Add(node);
        Model.IsPaused = true;
        try
        {
            await Debug.WaitIfPausedAsync(node.Id, _executionCts?.Token ?? CancellationToken.None);
        }
        finally
        {
            Model.IsPaused = false;
        }
    }

    /// <summary>单步继续/释放断点（调试模式）。</summary>
    public void StepContinue()
    {
        Debug.Continue();
        Model.IsPaused = false;
    }

    /// <summary>停止执行。</summary>
    public void Stop()
    {
        if (!Model.IsRunning) return;
        _executionCts?.Cancel();
        Model.IsRunning = false;
        Model.IsPaused = false;
        Model.IsLoopRunning = false;
        Model.StatusText = "正在停止...";
        FlushLog();
    }

    /// <summary>循环执行流程图（无限循环，直到手动停止）。</summary>
    public async Task RunLoopAsync(Action<string> logCallback)
    {
        if (Model.IsRunning || Model.IsLoopRunning) return;

        // 前置校验：静态分析一次性报告
        if (!RunPreValidation(logCallback))
            return;

        Model.IsLoopRunning = true;
        Model.IsRunning = true;
        Model.CurrentLoopIndex = 0;
        _executionCts = new CancellationTokenSource();

        // 捕获最近一次循环的运行序号，供循环作用域外（停止/异常）的收尾消息补 #前缀
        long loopRunId = 0;

        try
        {
            ClearLog();
            logCallback("=== 开始循环执行 ===");
            logCallback($"循环模式：间隔 {Model.LoopInterval}ms");

            var iteration = 0;
            using (ConsoleOutputRouter.EnterScope(msg => Application.Current?.Dispatcher.Invoke(() => logCallback(msg))))
            {
                while (true)
                {
                    _executionCts.Token.ThrowIfCancellationRequested();

                    iteration++;
                    Model.CurrentLoopIndex = iteration;
                    logCallback($"--- 第 {iteration} 次循环开始 ---");
                    Model.StatusText = $"循环执行中... 第 {iteration} 次";

                    // 重置所有节点状态
                    foreach (var n in Nodes)
                        n.ResetStatus();

                    _executionOrder.Clear();
                    CurrentExecutionIndex = 0;

                    // 每次循环 = 一次独立运行，进入新的流程（容器级）+ 任务作用域（RunId 自增）
                    using (FlowLogContext.EnterProcess(ProcessName ?? FlowLogName))
                    using (FlowLogContext.EnterFlow(FlowLogName))
                    {
                        // 引擎执行轨迹同步落盘（NodeLog/节点 Console 已有独立落盘通道，避免重复写入）
                        void Panel(string msg) => PanelLog(msg, logCallback);

                        loopRunId = FlowLogContext.RunId;

                        await _engine.ExecuteAsync(
                            Nodes.ToList(),
                            Connections.ToList(),
                            _executionCts.Token,
                            Panel,
                            Model.IsDebugMode ? OnBeforeNodeExecute : null,
                            _executionOrder,
                            Journal);
                    }

                    logCallback($"--- 第 {iteration} 次循环完成 ---");

                    // 等待间隔时间
                    var interval = Model.LoopInterval;
                    if (interval > 0)
                    {
                        logCallback($"[等待 {interval}ms 后开始下一次循环...]");
                        try
                        {
                            await Task.Delay(interval, _executionCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            PanelLog("=== 已停止循环执行 ===", logCallback, loopRunId);
            Model.StatusText = "循环执行已停止";
        }
        catch (Exception ex)
        {
            PanelLog($"[错误] {ex.Message}", logCallback, loopRunId);
            Model.StatusText = "循环执行出错";
        }
        finally
        {
            FlushLog();
            Model.IsRunning = false;
            Model.IsLoopRunning = false;
            Model.IsDebugMode = false;
            Model.IsPaused = false;
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    /// <summary>
    /// 引擎执行轨迹统一处理（面板 + 流程日志文件）：
    /// 1. 剪枝噪声（[跳过]/[分支]）面板与文件均不输出，简化运行输出；
    /// 2. 引擎级消息（无节点）自动补 #运行序号 前缀，与节点级/NodeLog 格式统一；
    /// 3. 按消息内标签区分日志等级（[错误]/[类型错误]/[堆栈]→Error，[容错]/[重试]→Warning，其余→Information）。
    /// </summary>
    private static void PanelLog(string msg, Action<string> logCallback, long runId = 0)
    {
        if (msg.StartsWith("[跳过]") || msg.StartsWith("[分支]")) return;

        // runId 参数用于流程作用域外的收尾消息（取消/异常）补 #前缀；作用域内传入 0 时取当前 RunId
        var id = runId > 0 ? runId : FlowLogContext.RunId;
        if (!msg.StartsWith("#") && id > 0)
            msg = $"#{id} {msg}";

        Application.Current?.Dispatcher.Invoke(() => logCallback(msg));

        var level = (msg.Contains("[错误]") || msg.Contains("[类型错误]") || msg.Contains("[堆栈]"))
            ? ELogLevel.Error
            : (msg.Contains("[容错]") || msg.Contains("[重试]"))
                ? ELogLevel.Warning
                : ELogLevel.Information;
        FlowLogContext.Write(msg, level);
    }

    /// <summary>停止循环执行。</summary>
    public void StopLoop()
    {
        if (!Model.IsLoopRunning) return;
        _executionCts?.Cancel();
        Model.IsLoopRunning = false;
        Model.IsRunning = false;
        Model.IsPaused = false;
        Model.StatusText = "正在停止循环执行...";
        FlushLog();
    }

    // ── 编辑/运行分离（P3）──

    /// <summary>
    /// 将当前画布编译为不可变 FlowProgram（纯数据快照）。
    /// 之后可用 FlowInstance.Create(program) 创建任意多个相互隔离的运行实例（并发/无头场景）。
    /// </summary>
    public FlowProgram CompileProgram()
        => FlowProgram.FromDocument(_persistence.BuildDocument(Model.TabTitle, Nodes, Connections, Model));

    // ── 静态校验（P2）──

    /// <summary>
    /// 运行前静态分析：一次性输出全部诊断（错误/警告/提示）。
    /// 存在错误时阻止执行并返回 false；警告与提示仅记录，不阻止执行。
    /// </summary>
    private bool RunPreValidation(Action<string> logCallback)
    {
        var report = FlowValidator.Analyze(Nodes.ToList(), Connections.ToList());
        foreach (var diag in report.Diagnostics)
        {
            var tag = diag.Severity switch
            {
                FlowDiagnosticSeverity.Error => "错误",
                FlowDiagnosticSeverity.Warning => "警告",
                _ => "提示"
            };
            logCallback($"[校验][{tag}] {diag.Message}");
        }

        if (report.HasErrors)
        {
            Model.StatusText = $"校验失败：{report.ErrorCount} 个错误";
            logCallback($"=== 校验失败，共 {report.ErrorCount} 个错误 ===");
            FlushLog();
            return false;
        }
        return true;
    }

    // ── 持久化 ──

    /// <summary>当前文件路径。</summary>
    public string? CurrentFilePath
    {
        get => Model.CurrentFilePath;
        set => Model.CurrentFilePath = value;
    }

    /// <inheritdoc />
    public bool IsBatchOperation { get; set; }

    /// <summary>保存流程图到文件。</summary>
    public bool Save(string? filePath = null)
    {
        var path = filePath ?? CurrentFilePath;
        if (string.IsNullOrEmpty(path))
            return false;

        try
        {
            var doc = _persistence.BuildDocument(Model.TabTitle, Nodes, Connections, Model);
            _persistence.Save(path, doc);
            CurrentFilePath = path;
            Model.IsModified = false;
            Model.StatusText = $"已保存：{path}";

            // 保存节点各自的外部数据（如视觉工具的参数文件）
            foreach (var vm in Nodes)
                vm.Model.OnFlowSaved();

            return true;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"保存失败：{ex.Message}");
            Model.StatusText = "保存失败";
            return false;
        }
    }

    /// <summary>从文件加载流程图。</summary>
    public bool Load(string filePath)
    {
        try
        {
            var doc = _persistence.Load(filePath);

            // 清空当前画布
        Connections.Clear();
        ClearNodes();
            UndoRedo.Clear();

            // 重建节点
            foreach (var nodeDoc in doc.Nodes)
            {
                var node = _persistence.RebuildNode(nodeDoc);
                AddNode(node);
            }

            // 还原连线
            foreach (var connDoc in doc.Connections)
            {
                Connections.Add(new ConnectionModel
                {
                    Id = connDoc.Id,
                    SourceNodeId = connDoc.SourceNodeId,
                    SourcePortId = connDoc.SourcePortId,
                    TargetNodeId = connDoc.TargetNodeId,
                    TargetPortId = connDoc.TargetPortId
                });
            }

            // 还原画布配置
            Model.ZoomLevel = doc.ZoomLevel;
            CurrentFilePath = filePath;
            Model.IsModified = false;
            Model.TabTitle = doc.Name;
            Model.StatusText = $"已加载：{filePath}";
            return true;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"加载失败：{ex.Message}");
            Model.StatusText = "加载失败";
            return false;
        }
    }

    // ── 撤销/重做快照 ──

    /// <summary>捕获当前画布状态快照（JSON 文档），用于撤销/重做。</summary>
    public string CaptureSnapshot()
    {
        var doc = _persistence.BuildDocument(Model.TabTitle, Nodes, Connections, Model);
        return JsonSerializer.Serialize(doc, SnapshotJsonOptions);
    }

    /// <summary>应用画布状态快照（JSON 文档），用于撤销/重做还原。</summary>
    public void ApplySnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        var doc = JsonSerializer.Deserialize<FlowChartDocumentModel>(json, SnapshotJsonOptions);
        if (doc == null) return;

        Connections.Clear();
        ClearNodes();

        foreach (var nodeDoc in doc.Nodes)
        {
            var node = _persistence.RebuildNode(nodeDoc);
            AddNode(node);
        }

        foreach (var connDoc in doc.Connections)
        {
            Connections.Add(new ConnectionModel
            {
                Id = connDoc.Id,
                SourceNodeId = connDoc.SourceNodeId,
                SourcePortId = connDoc.SourcePortId,
                TargetNodeId = connDoc.TargetNodeId,
                TargetPortId = connDoc.TargetPortId
            });
        }

        Model.ZoomLevel = doc.ZoomLevel;
        Model.TabTitle = doc.Name;
        Model.IsModified = true;
    }

    // ── 缩放 ──

    public void ZoomIn()
    {
        Model.ZoomLevel = Math.Round(Model.ZoomLevel + FlowChartModel.ZoomStep, 2);
    }

    public void ZoomOut()
    {
        Model.ZoomLevel = Math.Round(Model.ZoomLevel - FlowChartModel.ZoomStep, 2);
    }

    public void ZoomReset()
    {
        Model.ZoomLevel = 1.0;
    }

    public void ZoomToFit()
    {
        if (Nodes.Count == 0)
        {
            ZoomReset();
            return;
        }

        var minX = Nodes.Min(n => n.X);
        var minY = Nodes.Min(n => n.Y);
        var maxX = Nodes.Max(n => n.X + FlowChartModel.NodeWidth);
        var maxY = Nodes.Max(n => n.Y + FlowChartModel.NodeHeight);

        var contentWidth = maxX - minX + 200;
        var contentHeight = maxY - minY + 200;

        ZoomToFitRequested?.Invoke(this, (contentWidth, contentHeight, minX - 100, minY - 100));
    }

    public event EventHandler<(double contentWidth, double contentHeight, double offsetX, double offsetY)>? ZoomToFitRequested;

    // ── 辅助 ──

    /// <summary>追加日志到输出面板（写入缓冲，按 LogFlushIntervalMs 节流刷新 UI）。</summary>
    public void AppendLog(string message)
    {
        bool shouldFlush;
        lock (_logGate)
        {
            // 输出面板每行统一加时间前缀（日志文件本身含时间戳，无需在此添加）
            _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            if (_logBuilder.Length > MaxLogChars)
                _logBuilder.Remove(0, _logBuilder.Length / 2);

            shouldFlush = Stopwatch.GetElapsedTime(_lastLogFlushTimestamp).TotalMilliseconds >= LogFlushIntervalMs;
            if (shouldFlush)
                _lastLogFlushTimestamp = Stopwatch.GetTimestamp();
        }
        if (shouldFlush)
            FlushLog();
    }

    /// <summary>立即将日志缓冲冲刷到输出面板（节流期间的最后一批日志在执行结束时调用）。</summary>
    public void FlushLog()
    {
        string text;
        lock (_logGate)
        {
            text = _logBuilder.ToString();
            _lastLogFlushTimestamp = Stopwatch.GetTimestamp();
        }
        Model.OutputText = text;
    }

    /// <summary>清空日志缓冲与输出面板。</summary>
    private void ClearLog()
    {
        lock (_logGate)
            _logBuilder.Clear();
        Model.OutputText = string.Empty;
    }

    /// <summary>查找节点。</summary>
    public NodeViewModel? FindNode(string nodeId)
        => Nodes.FirstOrDefault(n => n.Id == nodeId);

    /// <summary>清空画布。</summary>
    public void ClearAll()
    {
        Connections.Clear();
        ClearNodes();
        UndoRedo.Clear();
        ClearLog();
        Model.StatusText = "已清空";
        Model.CurrentFilePath = null;
        Model.IsModified = false;
    }

    // ── ToolBlock 辅助方法 ──

    /// <summary>从节点文档重建节点模型（用于 ToolBlock 内部节点还原）。</summary>
    public NodeModel RebuildNodeFromDocument(NodeDocumentModel doc)
        => _persistence.RebuildNode(doc);

    /// <summary>从节点模型构建节点文档（用于 ToolBlock 内部节点序列化）。</summary>
    public NodeDocumentModel BuildNodeDocument(NodeModel node)
        => _persistence.BuildNodeDocument(node);

    // ── 节点管理 ──

    /// <summary>添加节点并设置其 Service 属性。</summary>
    public void AddNode(NodeViewModel nodeVm)
    {
        nodeVm.Model.Service = this;
        Nodes.Add(nodeVm);
        nodeVm.Model.OnAttached(this);
    }

    /// <summary>添加节点模型并返回 NodeViewModel。</summary>
    public NodeViewModel AddNode(NodeModel model)
    {
        model.Service = this;
        var vm = new NodeViewModel(model);
        Nodes.Add(vm);
        model.OnAttached(this);
        return vm;
    }

    /// <summary>从画布移除节点，并通知节点生命周期回调。</summary>
    public void RemoveNode(NodeViewModel nodeVm)
    {
        nodeVm.Model.OnDetached();
        Nodes.Remove(nodeVm);
    }

    /// <summary>清空画布上的全部节点，并逐个触发卸载回调。作为批量操作期间禁止移除共享资源（如视觉工具）。</summary>
    public void ClearNodes()
    {
        IsBatchOperation = true;
        try
        {
            foreach (var vm in Nodes.ToList())
                RemoveNode(vm);
        }
        finally
        {
            IsBatchOperation = false;
        }
    }
}
