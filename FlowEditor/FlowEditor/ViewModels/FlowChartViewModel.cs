using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
using FlowEditor.Services.Documents;
using FlowEditor.Services.Editing;
using FlowEditor.Services.Execution;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.ViewModels;

/// <summary>
/// 流程图主 ViewModel。
/// 依赖均通过构造器注入（IoC），分别使用画布、执行、文档和输出模块，
/// 管理选中状态、节点操作、连线绘制、流程编排和撤销重做等表现层状态。
/// 所有用户交互均由 RelayCommand 驱动，View 层不包含业务逻辑。
/// </summary>
public partial class FlowChartViewModel : ObservableObject
{
    private readonly FlowChartService _session;
    private readonly FlowExecutionCoordinator _execution;
    private readonly FlowDocumentSession _documents;
    private readonly FlowCanvasSession _canvas;
    private readonly FlowOutputBuffer _output;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly ConnectionService _connectionService;

    /// <summary>流程图运行时模型。View 直接绑定到此模型属性。</summary>
    public FlowChartModel Model => _session.Model;

    /// <summary>设置流程名（容器向下传播，供节点日志按「流程名\任务名」路由）。</summary>
    public void SetProcessName(string? name) => _session.ProcessName = name;

    /// <summary>画布上所有节点。</summary>
    public ObservableCollection<NodeViewModel> Nodes => _session.Nodes;

    /// <summary>所有连线。</summary>
    public ObservableCollection<ConnectionModel> Connections => _session.Connections;

    /// <summary>撤销/重做服务。</summary>
    public IUndoRedoService UndoRedo => _session.UndoRedo;

    #region 构造函数

    /// <summary>创建单个流程标签页 ViewModel。</summary>
    /// <param name="service">独立流程会话模块。</param>
    /// <param name="exportService">画布导出模块。</param>
    /// <param name="dialogService">宿主对话框模块。</param>
    /// <param name="connectionService">连线规则模块。</param>
    public FlowChartViewModel(
        FlowChartService service,
        IExportService exportService,
        IDialogService dialogService,
        ConnectionService connectionService)
    {
        _session = service ?? throw new ArgumentNullException(nameof(service));
        _execution = service.Execution;
        _documents = service.Documents;
        _canvas = service.Canvas;
        _output = service.Output;
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        InitializeCommands();

        // 节点集合变化时刷新命令状态
        Nodes.CollectionChanged += (_, _) => NotifyCanExecuteChanged();
        Connections.CollectionChanged += (_, _) => Model.IsModified = true;
        UndoRedo.StackChanged += NotifyCanExecuteChanged;
        Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FlowChartModel.IsRunning))
                NotifyCanExecuteChanged();
        };
    }


    #endregion

    #region 节点操作

    /// <summary>确保节点名称在当前画布上唯一，追加数字后缀。</summary>
    private void EnsureUniqueName(NodeModel node)
    {
        var baseName = node.Name;
        var existingNames = Nodes.Select(n => n.Name).ToHashSet();

        int suffix = 1;
        while (existingNames.Contains($"{baseName}_{suffix}"))
            suffix++;
        node.Name = $"{baseName}_{suffix}";
    }

    /// <summary>添加节点到画布。</summary>
    public void AddNode(NodePaletteItem? paletteItem)
    {
        if (paletteItem == null) return;
        var node = paletteItem.CreateNode();
        EnsureUniqueName(node);
        node.X = 200 + Nodes.Count * 40.0;
        node.Y = 150 + Nodes.Count * 30.0;

        var action = CreateAction(UndoRedoActionType.NodeAdded, $"添加节点: {node.Name}", nodeId: node.Id);
        var added = _canvas.AddNode(node);
        _canvas.RefreshExecutionMembership(added);
        action.AfterData = _documents.CaptureSnapshot();
        UndoRedo.Push(action);

        Model.IsModified = true;
        Model.StatusText = $"已添加节点：{node.Name}";
        RecalculateCanvasSize();
    }

    /// <summary>在指定画布坐标添加节点。</summary>
    public void AddNodeAt(NodePaletteItem paletteItem, Point canvasPosition)
    {
        var node = paletteItem.CreateNode();
        EnsureUniqueName(node);
        node.X = canvasPosition.X;
        node.Y = canvasPosition.Y;

        var action = CreateAction(UndoRedoActionType.NodeAdded, $"添加节点: {node.Name}", nodeId: node.Id);
        var added = _canvas.AddNode(node);
        _canvas.RefreshExecutionMembership(added);
        action.AfterData = _documents.CaptureSnapshot();
        UndoRedo.Push(action);

        Model.IsModified = true;
        Model.StatusText = $"已添加节点：{node.Name}";
        RecalculateCanvasSize();
    }

    /// <summary>
    /// 从持久化文档加载节点，跳过名称唯一性检查（节点名称在保存时已唯一）。
    /// 属于还原操作，不写入撤销历史。
    /// </summary>
    public NodeViewModel LoadNodeModel(NodeModel node)
    {
        _canvas.AddNode(node);
        Model.IsModified = false;
        RecalculateCanvasSize();
        return Nodes.Last();
    }

    /// <summary>删除选中的节点及其相关连线。</summary>
    private void DeleteSelected()
    {
        var selected = SelectedNodes.Count > 0
            ? SelectedNodes.ToList()
            : SelectedNode == null ? [] : [SelectedNode];
        if (selected.Count == 0) return;
        var selectedIds = selected.Select(n => n.Id).ToHashSet();
        var action = CreateAction(UndoRedoActionType.NodeDeleted, $"删除 {selected.Count} 个节点");
        var toRemove = Connections.Where(c =>
            selectedIds.Contains(c.SourceNodeId) || selectedIds.Contains(c.TargetNodeId)).ToList();
        foreach (var conn in toRemove)
            Connections.Remove(conn);
        foreach (var node in selected)
            _canvas.RemoveNode(node);
        foreach (var remaining in Nodes.Where(n =>
                     n.Model.ParentExecutionContainerId != null &&
                     selectedIds.Contains(n.Model.ParentExecutionContainerId)).ToList())
        {
            remaining.Model.ParentExecutionContainerId = null;
            remaining.Model.ExecutionLaneId = null;
        }
        foreach (var remaining in Nodes.Where(n => n.Model is not InactiveNodeContainerNode).ToList())
            _canvas.RefreshExecutionMembership(remaining);
        action.AfterData = _documents.CaptureSnapshot();
        UndoRedo.Push(action);

        SetSelectedNodes([]);
        Model.IsModified = true;
        Model.StatusText = $"已删除 {selected.Count} 个节点";
        RecalculateCanvasSize();
    }

    /// <summary>删除选中的连线。</summary>
    public void DeleteConnection(ConnectionModel? conn)
    {
        if (conn == null) return;

        // 释放目标节点的数据绑定属性
        var targetVm = _canvas.FindNode(conn.TargetNodeId);
        if (targetVm != null)
        {
            var targetModel = targetVm.Model;
            if (targetModel is IDataBindingProvider bindingProvider)
            {
                var bindings = bindingProvider.GetDataBindings();
                var binding = bindings.FirstOrDefault(b => b.TargetPortId == conn.TargetPortId);
                if (binding != null)
                {
                    // 释放绑定：解除输入端口的绑定状态
                    bindingProvider.RemoveDataBinding(binding.BindingId);
                    Model.StatusText = $"已释放绑定: {binding.UpstreamNodeName}.{binding.PropertyName}";
                    return; // RemoveDataBinding 已处理连线删除
                }
            }
        }

        var action = CreateAction(UndoRedoActionType.ConnectionDeleted, "删除连线", connectionId: conn.Id);
        if (SelectedConnection == conn)
            SelectedConnection = null;
        Connections.Remove(conn);
        action.AfterData = _documents.CaptureSnapshot();
        UndoRedo.Push(action);

        Model.IsModified = true;
    }

    // ── 节点右键菜单命令 ──

    /// <summary>为节点新增输入端口。</summary>
    private void AddInputPort(NodeViewModel? node)
    {
        if (node == null) return;
        node.Model.AddCustomInputPort($"Input_{node.Model.CustomInputPorts.Count + 1}");
        Model.IsModified = true;
        Model.StatusText = $"已为节点 [{node.Name}] 新增输入端口";
    }

    /// <summary>为节点新增输出端口（自动绑定第一个可绑定属性）。</summary>
    private void AddOutputPort(NodeViewModel? node)
    {
        if (node == null) return;
        var properties = node.Model.GetBindableProperties();
        var portName = $"Output_{node.Model.CustomOutputPorts.Count + 1}";
        if (properties.Count > 0)
            node.Model.AddCustomOutputPort(portName, properties[0]);
        else
            node.Model.AddCustomOutputPort(portName);
        Model.IsModified = true;
        Model.StatusText = $"已为节点 [{node.Name}] 新增输出端口";
    }

    /// <summary>设置节点权限。</summary>
    private void SetPermission(NodeViewModel? node, NodePermission permission)
    {
        if (node == null) return;
        node.Model.Permission = permission;
        Model.IsModified = true;
    }

    /// <summary>把节点移入不执行容器并清理不再合法的作用域连线。</summary>
    private void MoveToInactiveContainer(NodeViewModel? node)
    {
        if (node == null || node.IsExcludedFromExecution) return;
        var container = Nodes.FirstOrDefault(n => n.Model is InactiveNodeContainerNode);
        if (container == null)
        {
            Model.StatusText = "请先从流程工具箱添加不执行容器";
            return;
        }
        var action = CreateAction(UndoRedoActionType.NodeMoved, $"移入不执行容器: {node.Name}", node.Id);
        foreach (var connection in Connections.Where(c =>
                     c.SourceNodeId == node.Id || c.TargetNodeId == node.Id).ToList())
            Connections.Remove(connection);

        node.IsExcludedFromExecution = true;
        node.Model.ParentExecutionContainerId = null;
        node.Model.ExecutionLaneId = null;
        node.X = container.X + 20;
        node.Y = container.Y + 80 + Nodes.Count(n => n.IsExcludedFromExecution && n != node && n.Model is not InactiveNodeContainerNode) * 130;
        action.AfterData = _documents.CaptureSnapshot();
        UndoRedo.Push(action);
        Model.IsModified = true;
        Model.StatusText = $"[{node.Name}] 已移入不执行节点容器";
    }

    /// <summary>把节点从不执行容器恢复到活动画布。</summary>
    private void RestoreFromInactiveContainer(NodeViewModel? node)
    {
        if (node == null || !node.IsExcludedFromExecution) return;
        var action = CreateAction(UndoRedoActionType.NodeMoved, $"移回执行画布: {node.Name}", node.Id);
        node.IsExcludedFromExecution = false;
        node.X = Math.Max(320, node.X + 300);
        action.AfterData = _documents.CaptureSnapshot();
        UndoRedo.Push(action);
        Model.IsModified = true;
        Model.StatusText = $"[{node.Name}] 已移回执行画布";
        RecalculateCanvasSize();
    }

    /// <summary>容器调整大小后重新计算成员归属和画布范围。</summary>
    public void RefreshContainerMembership(NodeViewModel container)
    {
        _canvas.RefreshAllExecutionMembership();
        Model.IsModified = true;
        RecalculateCanvasSize();
    }

    #endregion

}
