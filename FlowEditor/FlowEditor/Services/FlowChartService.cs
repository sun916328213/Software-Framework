using System.Collections.ObjectModel;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services.Execution;
using FlowEditor.Services.Documents;
using FlowEditor.Services.Editing;
using FlowEditor.ViewModels;

namespace FlowEditor.Services;

/// <summary>
/// 流程图服务。
/// 声明并持有 FlowChartModel，提供节点/连线集合访问及核心业务方法。
/// 依赖通过构造器注入（IoC），不直接创建依赖对象，便于测试与解耦。
/// </summary>
public class FlowChartService : IFlowChartService
{
    /// <summary>流程图运行时模型。ViewModel 和 View 直接绑定到此模型属性。</summary>
    public FlowChartModel Model { get; } = new();

    /// <summary>画布上所有节点。</summary>
    public ObservableCollection<NodeViewModel> Nodes { get; } = new();

    /// <summary>所有连线。</summary>
    public ObservableCollection<ConnectionModel> Connections { get; } = new();

    /// <summary>撤销/重做服务。</summary>
    public IUndoRedoService UndoRedo { get; }

    /// <summary>宿主提供的节点专用编辑器启动器。</summary>
    public INodeEditorLauncher NodeEditorLauncher { get; }

    /// <summary>宿主提供的节点信息提示通道。</summary>
    public INodeNotificationSink NodeNotifications { get; }

    private readonly FlowDocumentMapper _documentMapper;
    private readonly FlowOutputBuffer _output;
    private readonly FlowExecutionCoordinator _execution;
    private readonly FlowDocumentSession _documents;
    private readonly FlowCanvasSession _canvas;

    /// <summary>创建流程图应用模块。</summary>
    /// <param name="documentStore">流程文档存储 adapter。</param>
    /// <param name="engine">流程执行引擎。</param>
    /// <param name="dialogService">文件及确认对话框模块。</param>
    /// <param name="nodeEditorLauncher">节点专用编辑器 adapter；空值时使用无头 adapter。</param>
    /// <param name="nodeNotifications">节点信息提示 adapter；空值时使用无头 adapter。</param>
    /// <param name="uiDispatcher">宿主 UI 线程调度 adapter；空值时在当前线程执行。</param>
    /// <param name="documentMapper">运行时模型与流程文档之间的 mapper。</param>
    public FlowChartService(
        IFlowDocumentStore documentStore,
        FlowChartExecutionEngine engine,
        IDialogService dialogService,
        INodeEditorLauncher? nodeEditorLauncher = null,
        INodeNotificationSink? nodeNotifications = null,
        IUiDispatcher? uiDispatcher = null,
        FlowDocumentMapper? documentMapper = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(dialogService);
        NodeEditorLauncher = nodeEditorLauncher ?? NullNodeInteraction.Instance;
        NodeNotifications = nodeNotifications ?? NullNodeInteraction.Instance;
        var dispatcher = uiDispatcher ?? ImmediateUiDispatcher.Instance;
        _documentMapper = documentMapper ?? new FlowDocumentMapper();
        _output = new FlowOutputBuffer(text => Model.OutputText = text, dispatcher);
        UndoRedo = new UndoRedoService();
        _canvas = new FlowCanvasSession(this, Model, Nodes, Connections, UndoRedo, _output);
        _execution = new FlowExecutionCoordinator(
            Model, Nodes, Connections, engine, _documentMapper, dispatcher, _output,
            () => ProcessName);
        _documents = new FlowDocumentSession(
            Model, Nodes, Connections, documentStore, _documentMapper, dialogService, UndoRedo,
            node => _canvas.AddNode(node), _canvas.ClearNodes);
    }

    /// <summary>当前执行节点索引（用于单步调试）。</summary>
    public int CurrentExecutionIndex => _execution.CurrentExecutionIndex;

    /// <summary>流程名（宿主设置，如 AutoFlow / InitializeFlow）；为节点日志的上级目录。</summary>
    public string? ProcessName { get; set; }

    /// <summary>调试控制器（P4）：断点与单步的 TCS 门闩。</summary>
    public DebugController Debug => _execution.Debug;

    /// <summary>画布执行的日志/数据追踪器（P4）：记录节点事件与连线传值。</summary>
    public ExecutionJournal Journal => _execution.Journal;

    // ── 执行模块委托 ──

    /// <summary>执行当前流程。</summary>
    public Task RunAsync(Action<string> logCallback, bool debugMode = false) =>
        _execution.RunAsync(logCallback, debugMode);

    /// <summary>释放调试暂停。</summary>
    public void StepContinue() => _execution.StepContinue();

    /// <summary>仅执行选中节点一次。</summary>
    public Task StepContinueAsync(NodeViewModel node, Action<string> logCallback) =>
        _execution.StepContinueAsync(node, logCallback);

    /// <summary>从选中节点恢复或跳转执行。</summary>
    public Task ContinueFromNodeAsync(NodeViewModel node, Action<string> logCallback) =>
        _execution.ContinueFromNodeAsync(node, logCallback);

    /// <summary>停止当前执行。</summary>
    public void Stop() => _execution.Stop();

    /// <summary>循环执行当前流程。</summary>
    public Task RunLoopAsync(Action<string> logCallback) =>
        _execution.RunLoopAsync(logCallback);

    /// <summary>停止循环执行。</summary>
    public void StopLoop() => _execution.StopLoop();

    /// <summary>编译当前流程为不可变程序。</summary>
    public FlowProgram CompileProgram() => _execution.CompileProgram();
    // ── 文档模块委托 ──

    /// <summary>当前文件路径。</summary>
    public string? CurrentFilePath
    {
        get => Model.CurrentFilePath;
        set => Model.CurrentFilePath = value;
    }

    /// <inheritdoc />
    public bool IsBatchOperation
    {
        get => _canvas.IsBatchOperation;
        set => _canvas.IsBatchOperation = value;
    }

    /// <summary>保存当前流程文档。</summary>
    public bool Save(string? filePath = null) => _documents.Save(filePath);

    /// <summary>从文件加载流程文档。</summary>
    public bool Load(string filePath) => _documents.Load(filePath);

    /// <summary>捕获撤销/重做快照。</summary>
    public string CaptureSnapshot() => _documents.CaptureSnapshot();

    /// <summary>应用撤销/重做快照。</summary>
    public void ApplySnapshot(string json) => _documents.ApplySnapshot(json);
    // ── 画布模块委托 ──

    /// <summary>放大画布。</summary>
    public void ZoomIn() => _canvas.ZoomIn();

    /// <summary>缩小画布。</summary>
    public void ZoomOut() => _canvas.ZoomOut();

    /// <summary>重置画布缩放。</summary>
    public void ZoomReset() => _canvas.ZoomReset();

    /// <summary>请求自适应全部画布内容。</summary>
    public void ZoomToFit() => _canvas.ZoomToFit();

    /// <summary>转发画布自适应请求。</summary>
    public event EventHandler<(double contentWidth, double contentHeight, double offsetX, double offsetY)>? ZoomToFitRequested
    {
        add => _canvas.ZoomToFitRequested += value;
        remove => _canvas.ZoomToFitRequested -= value;
    }

    // ── 辅助 ──

    /// <summary>追加日志到输出缓冲，并按节流策略刷新输出栏。</summary>
    public void AppendLog(string message) => _output.Append(message);

    /// <summary>立即将日志缓冲冲刷到输出面板（节流期间的最后一批日志在执行结束时调用）。</summary>
    public void FlushLog() => _output.Flush();

    /// <summary>清空日志缓冲与输出面板；已经排队的节流刷新只会发布清空后的内容。</summary>
    public void ClearOutput() => _output.Clear();

    /// <summary>查找节点。</summary>
    public NodeViewModel? FindNode(string nodeId) => _canvas.FindNode(nodeId);

    /// <summary>清空画布。</summary>
    public void ClearAll() => _canvas.ClearAll();

    // ── ToolBlock 辅助方法 ──

    /// <summary>从节点文档重建节点模型（用于 ToolBlock 内部节点还原）。</summary>
    public NodeModel RebuildNodeFromDocument(NodeDocumentModel doc)
        => _documentMapper.ToNode(doc);

    /// <summary>从节点模型构建节点文档（用于 ToolBlock 内部节点序列化）。</summary>
    public NodeDocumentModel BuildNodeDocument(NodeModel node)
        => _documentMapper.ToNodeDocument(node);

    // ── 节点管理 ──

    /// <summary>添加节点并设置其 Service 属性。</summary>
    public void AddNode(NodeViewModel nodeVm) => _canvas.AddNode(nodeVm);

    /// <summary>添加节点模型并返回 NodeViewModel。</summary>
    public NodeViewModel AddNode(NodeModel model) => _canvas.AddNode(model);

    /// <summary>从画布移除节点，并通知节点生命周期回调。</summary>
    public void RemoveNode(NodeViewModel nodeVm) => _canvas.RemoveNode(nodeVm);

    /// <summary>清空画布上的全部节点，并逐个触发卸载回调。作为批量操作期间禁止移除共享资源（如视觉工具）。</summary>
    public void ClearNodes() => _canvas.ClearNodes();
}
