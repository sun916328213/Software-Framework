using System.Collections.ObjectModel;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Services.Documents;
using FlowEditor.Services.Editing;
using FlowEditor.Services.Execution;
using FlowEditor.ViewModels;

namespace FlowEditor.Services;

/// <summary>
/// 单个流程标签页的组合根和节点上下文。
/// 负责创建并连接执行、文档、画布和输出模块，但不转发这些模块的操作接口。
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

    /// <summary>当前会话的输出缓冲模块。</summary>
    internal FlowOutputBuffer Output { get; }

    /// <summary>当前会话的执行协调模块。</summary>
    internal FlowExecutionCoordinator Execution { get; }

    /// <summary>当前会话的文档事务模块。</summary>
    internal FlowDocumentSession Documents { get; }

    /// <summary>当前会话的画布编辑模块。</summary>
    internal FlowCanvasSession Canvas { get; }

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
        var mapper = documentMapper ?? new FlowDocumentMapper();
        Output = new FlowOutputBuffer(text => Model.OutputText = text, dispatcher);
        UndoRedo = new UndoRedoService();
        Canvas = new FlowCanvasSession(this, Model, Nodes, Connections, UndoRedo, Output);
        Execution = new FlowExecutionCoordinator(
            Model, Nodes, Connections, engine, mapper, dispatcher, Output,
            () => ProcessName);
        Documents = new FlowDocumentSession(
            Model, Nodes, Connections, documentStore, mapper, dialogService, UndoRedo,
            node => Canvas.AddNode(node), Canvas.ClearNodes);
    }

    /// <summary>流程名（宿主设置，如 AutoFlow / InitializeFlow）；为节点日志的上级目录。</summary>
    public string? ProcessName { get; set; }

    /// <inheritdoc />
    IEnumerable<NodeModel> IFlowChartService.Nodes => Nodes.Select(node => node.Model);

    /// <inheritdoc />
    NodeModel? IFlowChartService.FindNode(string nodeId) => Canvas.FindNode(nodeId)?.Model;

    /// <summary>从节点文档重建节点模型（用于 ToolBlock 内部节点还原）。</summary>
    public NodeModel RebuildNodeFromDocument(NodeDocumentModel doc)
        => Documents.RebuildNodeFromDocument(doc);

}
