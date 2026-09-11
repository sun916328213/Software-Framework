using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.ViewModels;

/// <summary>
/// 流程图主 ViewModel。
/// 依赖均通过构造器注入（IoC），通过 FlowChartService 接口访问模型，
/// 管理选中状态、节点操作、连线绘制、执行流程、持久化、撤销重做等业务逻辑。
/// 所有用户交互均由 RelayCommand 驱动，View 层不包含业务逻辑。
/// </summary>
public partial class FlowChartViewModel : ObservableObject
{
    private readonly IFlowChartService Service;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly ConnectionService _connectionService;

    /// <summary>流程图运行时模型。View 直接绑定到此模型属性。</summary>
    public FlowChartModel Model => Service.Model;

    /// <summary>设置流程名（容器向下传播，供节点日志按「流程名\任务名」路由）。</summary>
    public void SetProcessName(string? name) => Service.ProcessName = name;

    /// <summary>画布上所有节点。</summary>
    public ObservableCollection<NodeViewModel> Nodes => Service.Nodes;

    /// <summary>所有连线。</summary>
    public ObservableCollection<ConnectionModel> Connections => Service.Connections;

    /// <summary>撤销/重做服务。</summary>
    public IUndoRedoService UndoRedo => Service.UndoRedo;

    #region 构造函数

    /// <summary>创建单个流程标签页 ViewModel。</summary>
    /// <param name="service">独立流程会话模块。</param>
    /// <param name="exportService">画布导出模块。</param>
    /// <param name="dialogService">宿主对话框模块。</param>
    /// <param name="connectionService">连线规则模块。</param>
    public FlowChartViewModel(
        IFlowChartService service,
        IExportService exportService,
        IDialogService dialogService,
        ConnectionService connectionService)
    {
        Service = service ?? throw new ArgumentNullException(nameof(service));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        InitializeCommands();

        // 节点集合变化时刷新命令状态
        Nodes.CollectionChanged += (_, _) => NotifyCanExecuteChanged();
        Connections.CollectionChanged += (_, _) => Model.IsModified = true;
        Service.UndoRedo.StackChanged += NotifyCanExecuteChanged;
        Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FlowChartModel.IsRunning))
                NotifyCanExecuteChanged();
        };
    }


    #endregion

    #region 选中状态

    private NodeViewModel? _selectedNode;
    /// <summary>当前选中的节点。</summary>
    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (EqualityComparer<NodeViewModel?>.Default.Equals(_selectedNode, value)) return;

            _selectedNode = value;
            SelectedNodes.Clear();
            if (value != null) SelectedNodes.Add(value);
            foreach (var node in Nodes)
                node.IsSelected = node == value;

            // 先让节点的选中视觉生效，再通知属性面板等较重的订阅者。
            OnPropertyChanged(nameof(SelectedNode));
            StepContinueCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>多选节点集合（框选时使用）。</summary>
    public ObservableCollection<NodeViewModel> SelectedNodes { get; } = new();

    /// <summary>用一组节点替换当前选择；集合中最后一个节点作为属性面板主节点。</summary>
    public void SetSelectedNodes(IEnumerable<NodeViewModel> nodes)
    {
        var selection = nodes.Distinct().ToList();
        var selectedSet = selection.ToHashSet();
        SelectedNodes.Clear();
        foreach (var node in selection)
            SelectedNodes.Add(node);

        _selectedNode = selection.LastOrDefault();
        foreach (var node in Nodes)
            node.IsSelected = selectedSet.Contains(node);

        // SelectedNode 的订阅者可能会重建属性面板，因此必须在视觉状态同步之后通知。
        OnPropertyChanged(nameof(SelectedNode));
        StepContinueCommand.NotifyCanExecuteChanged();
    }

    /// <summary>切换单个节点的多选状态。</summary>
    public void ToggleNodeSelection(NodeViewModel node)
    {
        var selection = SelectedNodes.ToList();
        if (selection.Contains(node)) selection.Remove(node);
        else selection.Add(node);
        SetSelectedNodes(selection);
    }

    private Dictionary<NodeViewModel, Point>? _groupDragOrigins;
    private Point _groupDragStart;

    public void BeginNodeDrag(NodeViewModel node, Point mousePosition)
    {
        if (!node.IsSelected) SetSelectedNodes([node]);
        IEnumerable<NodeViewModel> selected = SelectedNodes.Count > 0 ? SelectedNodes : [node];
        var movingContainer = IsCanvasContainer(node);
        if (node.Model is InactiveNodeContainerNode)
        {
            selected = selected.Concat(Nodes.Where(candidate =>
                candidate != node && IsInsideContainer(node, candidate))).Distinct();
        }
        else if (node.Model is IExecutionScopeContainer)
        {
            selected = selected.Concat(Nodes.Where(candidate =>
                candidate != node && IsExecutionDescendantOf(candidate, node.Id))).Distinct();
        }
        _groupDragStart = mousePosition;
        _groupDragOrigins = selected
            .Where(n => movingContainer || n.IsDraggable)
            .ToDictionary(n => n, n => new Point(n.X, n.Y));
        node.BeginDrag(mousePosition);
    }

    public void UpdateNodeDrag(NodeViewModel node, Point mousePosition)
    {
        if (_groupDragOrigins == null) return;
        var delta = mousePosition - _groupDragStart;
        foreach (var (selected, origin) in _groupDragOrigins)
        {
            selected.X = Math.Max(0, origin.X + delta.X);
            selected.Y = Math.Max(0, origin.Y + delta.Y);
        }
    }

    public void EndNodeDrag(NodeViewModel node)
    {
        node.EndDrag();
        if (_groupDragOrigins != null)
        {
            foreach (var moved in _groupDragOrigins.Keys.ToList())
                RefreshNodeExecutionMembership(moved);
        }
        _groupDragOrigins = null;
        Model.IsModified = true;
        RecalculateCanvasSize();
    }

    private ConnectionModel? _selectedConnection;
    /// <summary>当前选中的连线。</summary>
    public ConnectionModel? SelectedConnection
    {
        get => _selectedConnection;
        set => SetProperty(ref _selectedConnection, value);
    }

    #endregion

    #region 临时连线状态

    private bool _isDrawingConnection;
    public bool IsDrawingConnection
    {
        get => _isDrawingConnection;
        private set => SetProperty(ref _isDrawingConnection, value);
    }

    private PortModel? _connectionSourcePort;
    public PortModel? ConnectionSourcePort
    {
        get => _connectionSourcePort;
        private set => SetProperty(ref _connectionSourcePort, value);
    }

    private Geometry? _pendingConnectionGeometry;
    public Geometry? PendingConnectionGeometry
    {
        get => _pendingConnectionGeometry;
        private set => SetProperty(ref _pendingConnectionGeometry, value);
    }

    #endregion

    #region 复制粘贴

    private readonly List<NodeDocumentModel> _clipboardNodes = new();
    private readonly List<ConnectionDocumentModel> _clipboardConnections = new();

    /// <summary>复制选中的节点。</summary>
    private void CopySelected()
    {
        var selected = SelectedNodes.Count > 0
            ? SelectedNodes.ToList()
            : SelectedNode == null ? [] : [SelectedNode];
        if (selected.Count == 0) return;

        _clipboardNodes.Clear();
        _clipboardNodes.AddRange(selected.Select(n => Service.BuildNodeDocument(n.Model)));
        var selectedIds = selected.Select(n => n.Id).ToHashSet();
        _clipboardConnections.Clear();
        _clipboardConnections.AddRange(Connections
            .Where(c => selectedIds.Contains(c.SourceNodeId) && selectedIds.Contains(c.TargetNodeId))
            .Select(c => new ConnectionDocumentModel
            {
                Id = c.Id,
                SourceNodeId = c.SourceNodeId,
                SourcePortId = c.SourcePortId,
                TargetNodeId = c.TargetNodeId,
                TargetPortId = c.TargetPortId
            }));
        Model.StatusText = $"已复制 {selected.Count} 个节点";
    }

    /// <summary>粘贴节点到画布中心。</summary>
    private void PasteAtCenter()
    {
        if (_clipboardNodes.Count == 0) return;
        var action = CreateAction(UndoRedoActionType.NodeAdded, $"粘贴 {_clipboardNodes.Count} 个节点");
        var nodeIdMap = new Dictionary<string, string>();
        var portIdMap = new Dictionary<string, string>();
        var pasted = new List<NodeViewModel>();

        foreach (var sourceDoc in _clipboardNodes)
        {
            var sourceId = sourceDoc.Id;
            var clone = CloneDocumentForPaste(sourceDoc);
            nodeIdMap[sourceId] = clone.Id;
            var newNode = Service.RebuildNodeFromDocument(clone);
            EnsureUniqueName(newNode);
            var vm = Service.AddNode(newNode);
            pasted.Add(vm);

            foreach (var (key, oldPortId) in sourceDoc.PortIdMap)
            {
                var separator = key.IndexOf(':');
                if (separator < 0) continue;
                var direction = key[..separator];
                var name = key[(separator + 1)..];
                var newPort = direction == "IN"
                    ? newNode.InputPorts.FirstOrDefault(p => p.Name == name)
                    : newNode.OutputPorts.FirstOrDefault(p => p.Name == name);
                if (newPort != null) portIdMap[oldPortId] = newPort.Id;
            }
        }

        foreach (var connection in _clipboardConnections)
        {
            if (!nodeIdMap.TryGetValue(connection.SourceNodeId, out var sourceNodeId) ||
                !nodeIdMap.TryGetValue(connection.TargetNodeId, out var targetNodeId) ||
                !portIdMap.TryGetValue(connection.SourcePortId, out var sourcePortId) ||
                !portIdMap.TryGetValue(connection.TargetPortId, out var targetPortId)) continue;
            Connections.Add(new ConnectionModel
            {
                SourceNodeId = sourceNodeId,
                SourcePortId = sourcePortId,
                TargetNodeId = targetNodeId,
                TargetPortId = targetPortId
            });
        }

        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);
        SetSelectedNodes(pasted);
        Model.IsModified = true;
        Model.StatusText = $"已粘贴 {pasted.Count} 个节点";
        RecalculateCanvasSize();
    }

    private static NodeDocumentModel CloneDocumentForPaste(NodeDocumentModel source)
    {
        var clone = new NodeDocumentModel
        {
            Id = Guid.NewGuid().ToString("N"),
            TypeName = source.TypeName,
            Name = source.Name + "_副本",
            X = source.X + 50,
            Y = source.Y + 50,
            IsInEnabled = source.IsInEnabled,
            IsOutEnabled = source.IsOutEnabled,
            Permission = source.Permission,
            IsExcludedFromExecution = source.IsExcludedFromExecution,
            ErrorPolicy = source.ErrorPolicy,
            RetryCount = source.RetryCount,
            RetryDelayMs = source.RetryDelayMs,
            TimeoutMs = source.TimeoutMs,
            Properties = new Dictionary<string, object?>(source.Properties),
            PortIdMap = new Dictionary<string, string>(source.PortIdMap)
        };
        clone.CustomInputPorts.AddRange(source.CustomInputPorts.Select(p => new CustomPortDocument
        {
            Id = Guid.NewGuid().ToString("N"), Name = p.Name, BoundProperty = p.BoundProperty,
            DataType = p.DataType
        }));
        clone.CustomOutputPorts.AddRange(source.CustomOutputPorts.Select(p => new CustomPortDocument
        {
            Id = Guid.NewGuid().ToString("N"), Name = p.Name, BoundProperty = p.BoundProperty,
            DataType = p.DataType
        }));
        return clone;
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
        var added = Service.AddNode(node);
        RefreshNodeExecutionMembership(added);
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

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
        var added = Service.AddNode(node);
        RefreshNodeExecutionMembership(added);
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

        Model.IsModified = true;
        Model.StatusText = $"已添加节点：{node.Name}";
        RecalculateCanvasSize();
    }

    /// <summary>
    /// 从已创建的节点模型添加节点到画布（用于 ToolBlock 内部数据加载）。
    /// </summary>
    public NodeViewModel AddNodeModel(NodeModel node)
    {
        EnsureUniqueName(node);

        var action = CreateAction(UndoRedoActionType.NodeAdded, $"添加节点: {node.Name}", nodeId: node.Id);
        Service.AddNode(node);
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

        Model.IsModified = true;
        Model.StatusText = $"已添加节点：{node.Name}";
        RecalculateCanvasSize();
        return Nodes.Last();
    }

    /// <summary>
    /// 从持久化文档加载节点，跳过名称唯一性检查（节点名称在保存时已唯一）。
    /// 属于还原操作，不写入撤销历史。
    /// </summary>
    public NodeViewModel LoadNodeModel(NodeModel node)
    {
        Service.AddNode(node);
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
            Service.RemoveNode(node);
        foreach (var remaining in Nodes.Where(n =>
                     n.Model.ParentExecutionContainerId != null &&
                     selectedIds.Contains(n.Model.ParentExecutionContainerId)).ToList())
        {
            remaining.Model.ParentExecutionContainerId = null;
            remaining.Model.ExecutionLaneId = null;
        }
        foreach (var remaining in Nodes.Where(n => n.Model is not InactiveNodeContainerNode).ToList())
            RefreshNodeExecutionMembership(remaining);
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

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
        var targetVm = Service.FindNode(conn.TargetNodeId);
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
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

        Model.IsModified = true;
    }

    /// <summary>新建空白流程图。</summary>
    private void NewFlow()
    {
        if (Model.IsModified)
        {
            var result = _dialogService.ShowConfirm("当前流程图有未保存的修改，要保存吗？", "新建流程");
            if (result == null) return;
            if (result == true) Save();
        }

        Service.ClearAll();
        Model.ZoomLevel = 1.0;
        RecalculateCanvasSize();
        Model.StatusText = "已新建空白流程";
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
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);
        Model.IsModified = true;
        Model.StatusText = $"[{node.Name}] 已移入不执行节点容器";
    }

    private void RestoreFromInactiveContainer(NodeViewModel? node)
    {
        if (node == null || !node.IsExcludedFromExecution) return;
        var action = CreateAction(UndoRedoActionType.NodeMoved, $"移回执行画布: {node.Name}", node.Id);
        node.IsExcludedFromExecution = false;
        node.X = Math.Max(320, node.X + 300);
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);
        Model.IsModified = true;
        Model.StatusText = $"[{node.Name}] 已移回执行画布";
        RecalculateCanvasSize();
    }

    /// <summary>根据节点中心位置刷新“不执行”状态以及执行容器/泳道归属。</summary>
    private void RefreshNodeExecutionMembership(NodeViewModel node)
    {
        if (node.Model is InactiveNodeContainerNode)
        {
            node.IsExcludedFromExecution = true;
            foreach (var candidate in Nodes.Where(n => n != node && n.Model is not InactiveNodeContainerNode).ToList())
                RefreshNodeExecutionMembership(candidate);
            return;
        }

        var inactiveContainer = Nodes.FirstOrDefault(container =>
            container.Model is InactiveNodeContainerNode &&
            IsInsideContainer(container, node));
        if (inactiveContainer != null)
        {
            var changed = !node.IsExcludedFromExecution || node.Model.ParentExecutionContainerId != null;
            node.IsExcludedFromExecution = true;
            node.Model.ParentExecutionContainerId = null;
            node.Model.ExecutionLaneId = null;
            if (changed)
            {
                RemoveConnectionsOutsideCurrentScope(node);
                Model.StatusText = $"节点 [{node.Name}] 已放入不执行容器";
            }
            return;
        }

        node.IsExcludedFromExecution = false;
        var executionContainer = Nodes
            .Where(container =>
                container != node &&
                container.Model is IExecutionScopeContainer &&
                !IsExecutionDescendantOf(container, node.Id) &&
                IsInsideContainer(container, node))
            .OrderBy(container => GetNodeWidth(container) * GetNodeHeight(container))
            .FirstOrDefault();

        var oldParentId = node.Model.ParentExecutionContainerId;
        var newParentId = executionContainer?.Id;
        var newLaneId = executionContainer == null ? null : ResolveLaneId(executionContainer, node);
        var membershipChanged = node.Model.ParentExecutionContainerId != newParentId ||
                                node.Model.ExecutionLaneId != newLaneId;
        node.Model.ParentExecutionContainerId = newParentId;
        node.Model.ExecutionLaneId = newLaneId;

        if (membershipChanged)
        {
            if (!ShouldPreserveLoopBoundaryConnections(oldParentId, newParentId))
                RemoveConnectionsOutsideCurrentScope(node);
            Model.StatusText = executionContainer == null
                ? $"节点 [{node.Name}] 已移回主流程"
                : $"节点 [{node.Name}] 已加入 [{executionContainer.Name}] / [{GetLaneName(executionContainer, newLaneId)}]";
        }
    }

    private bool ShouldPreserveLoopBoundaryConnections(string? oldParentId, string? newParentId)
    {
        if (oldParentId == newParentId) return true;
        var oldIsLoop = oldParentId != null && Nodes.Any(candidate =>
            candidate.Id == oldParentId && candidate.Model is LoopContainerNode);
        var newIsLoop = newParentId != null && Nodes.Any(candidate =>
            candidate.Id == newParentId && candidate.Model is LoopContainerNode);
        return (oldIsLoop && newParentId == null) || (newIsLoop && oldParentId == null);
    }

    private static bool IsInsideContainer(NodeViewModel container, NodeViewModel node)
    {
        var center = new Point(
            node.X + GetNodeWidth(node) / 2,
            node.Y + GetNodeHeight(node) / 2);
        return new Rect(container.X, container.Y, GetNodeWidth(container), GetNodeHeight(container))
            .Contains(center);
    }

    private static bool IsCanvasContainer(NodeViewModel node) =>
        node.Model is InactiveNodeContainerNode or IExecutionScopeContainer;

    private static double GetNodeWidth(NodeViewModel node) => node.Model switch
    {
        InactiveNodeContainerNode inactive => inactive.ContainerWidth,
        IExecutionScopeContainer execution => execution.ContainerWidth,
        _ => FlowChartModel.NodeWidth
    };

    private static double GetNodeHeight(NodeViewModel node) => node.Model switch
    {
        InactiveNodeContainerNode inactive => inactive.ContainerHeight,
        IExecutionScopeContainer execution => execution.ContainerHeight,
        _ => FlowChartModel.NodeHeight
    };

    private bool IsExecutionDescendantOf(NodeViewModel candidate, string containerId)
    {
        var parentId = candidate.Model.ParentExecutionContainerId;
        var visited = new HashSet<string>();
        while (parentId != null && visited.Add(parentId))
        {
            if (parentId == containerId) return true;
            parentId = Nodes.FirstOrDefault(node => node.Id == parentId)?.Model.ParentExecutionContainerId;
        }
        return false;
    }

    private static string? ResolveLaneId(NodeViewModel container, NodeViewModel node)
    {
        if (container.Model is not IExecutionScopeContainer scope || scope.ExecutionLanes.Count == 0)
            return null;
        const double headerHeight = 64;
        const double bottomMargin = 16;
        var contentHeight = Math.Max(1, scope.ContainerHeight - headerHeight - bottomMargin);
        var centerY = node.Y + GetNodeHeight(node) / 2;
        var relativeY = Math.Clamp(centerY - container.Y - headerHeight, 0, contentHeight - 0.001);
        var index = Math.Clamp((int)(relativeY / contentHeight * scope.ExecutionLanes.Count),
            0, scope.ExecutionLanes.Count - 1);
        return scope.ExecutionLanes[index].Id;
    }

    private static string GetLaneName(NodeViewModel container, string? laneId) =>
        container.Model is IExecutionScopeContainer scope
            ? scope.ExecutionLanes.FirstOrDefault(lane => lane.Id == laneId)?.Name ?? "未指定泳道"
            : "未指定泳道";

    private void RemoveConnectionsOutsideCurrentScope(NodeViewModel node)
    {
        foreach (var connection in Connections.Where(connection =>
                     connection.SourceNodeId == node.Id || connection.TargetNodeId == node.Id).ToList())
        {
            var otherId = connection.SourceNodeId == node.Id
                ? connection.TargetNodeId
                : connection.SourceNodeId;
            var other = Nodes.FirstOrDefault(candidate => candidate.Id == otherId);
            if (other == null ||
                node.IsExcludedFromExecution ||
                other.Model.ParentExecutionContainerId != node.Model.ParentExecutionContainerId ||
                other.Model.ExecutionLaneId != node.Model.ExecutionLaneId)
                Connections.Remove(connection);
        }
    }

    /// <summary>容器调整大小后重新计算成员归属和画布范围。</summary>
    public void RefreshContainerMembership(NodeViewModel container)
    {
        foreach (var candidate in Nodes.Where(node => node.Model is not InactiveNodeContainerNode).ToList())
            RefreshNodeExecutionMembership(candidate);
        Model.IsModified = true;
        RecalculateCanvasSize();
    }

    #endregion

    #region 执行命令

    private async Task RunAsync()
    {
        await Service.RunAsync(msg => AppendLog(msg), debugMode: false);
    }

    private bool CanRun() => !Model.IsRunning && Nodes.Count > 0;
    private void Stop() => Service.Stop();
    private bool CanStop() => Model.IsRunning;

    private async Task StepContinueSelectedAsync()
    {
        // 保留调试运行中的“继续”语义；普通状态下执行当前选中节点一次。
        if (Model.IsPaused)
        {
            Service.StepContinue();
            return;
        }

        var selected = SelectedNode;
        if (selected == null)
            return;

        // 流程中存在失败状态时，“继续”应采用恢复/跳转语义：所选节点成为新入口，
        // 可选择失败节点重试，也可选择它之前或之后的节点继续整条可达路径。
        if (Nodes.Any(node => node.Status == NodeStatus.Error))
            await Service.ContinueFromNodeAsync(selected, AppendLog);
        else
            await Service.StepContinueAsync(selected, AppendLog);
    }

    private bool CanStepContinue()
        => Model.IsPaused ||
           (!Model.IsRunning && SelectedNode is { IsExcludedFromExecution: false });

    /// <summary>从右键选中的节点恢复或跳转，并继续执行其后继路径。</summary>
    private Task ContinueFromNodeAsync(NodeViewModel? node)
        => node == null ? Task.CompletedTask : Service.ContinueFromNodeAsync(node, AppendLog);

    /// <summary>空闲时允许任意执行节点跳转；运行中只允许恢复并行容器内的独立支路。</summary>
    private bool CanContinueFromNode(NodeViewModel? node)
        => node is { IsExcludedFromExecution: false } &&
           (!Model.IsRunning ||
            (node.Model.ParentExecutionContainerId is { } parentId &&
             Service.FindNode(parentId)?.Model is ParallelContainerNode));

    private async Task LoopRunAsync()
    {
        await Service.RunLoopAsync(msg => AppendLog(msg));
    }

    private bool CanLoopRun() => !Model.IsRunning && !Model.IsLoopRunning && Nodes.Count > 0;
    private void LoopStop() => Service.StopLoop();
    private bool CanLoopStop() => Model.IsLoopRunning;

    #endregion

    #region 持久化命令

    /// <summary>保存流程图。</summary>
    private void Save()
    {
        if (!string.IsNullOrEmpty(Service.CurrentFilePath))
        {
            Service.Save();
            return;
        }
        SaveAs();
    }

    /// <summary>另存为。</summary>
    private void SaveAs()
    {
        var path = _dialogService.ShowSaveFileDialog(
            "流程图文件 (*.flow)|*.flow|所有文件 (*.*)|*.*",
            Model.TabTitle + ".flow",
            ".flow");
        if (path != null)
            Service.Save(path);
    }

    /// <summary>从文件加载流程图。</summary>
    private void Load()
    {
        var path = _dialogService.ShowOpenFileDialog("流程图文件 (*.flow)|*.flow|所有文件 (*.*)|*.*");
        if (path != null)
            Service.Load(path);
    }

    /// <summary>清空画布（二次确认）。</summary>
    private void ClearAll()
    {
        var confirmed = _dialogService.ConfirmYesNo(
            "确定要删除所有流程吗？此操作不可撤销！", "确认删除");
        if (confirmed)
        {
            Service.ClearAll();
            RecalculateCanvasSize();
        }
    }

    #endregion

    #region 撤销/重做

    /// <summary>创建撤销动作并捕获操作前快照。</summary>
    private UndoRedoAction CreateAction(
        UndoRedoActionType type, string description,
        string? nodeId = null, string? connectionId = null)
        => new()
        {
            ActionType = type,
            Description = description,
            NodeId = nodeId,
            ConnectionId = connectionId,
            BeforeData = Service.CaptureSnapshot()
        };

    private void Undo()
    {
        var action = Service.UndoRedo.Undo();
        if (action == null) return;
        // 保存当前状态作为重做目标
        action.AfterData = Service.CaptureSnapshot();
        Service.ApplySnapshot(action.BeforeData!);
        Model.StatusText = $"已撤销: {action.Description}";
        Model.IsModified = true;
        RecalculateCanvasSize();
        NotifyCanExecuteChanged();
    }

    private void Redo()
    {
        var action = Service.UndoRedo.Redo();
        if (action == null) return;
        // 保存当前状态作为撤销目标
        action.BeforeData = Service.CaptureSnapshot();
        Service.ApplySnapshot(action.AfterData!);
        Model.StatusText = $"已重做: {action.Description}";
        Model.IsModified = true;
        RecalculateCanvasSize();
        NotifyCanExecuteChanged();
    }

    #endregion

    #region 连线绘制

    public void StartConnection(PortModel sourcePort, Point startPos)
    {
        ConnectionSourcePort = sourcePort;
        IsDrawingConnection = true;
    }

    public void UpdateConnectionEndPoint(Point endPos)
    {
        if (!IsDrawingConnection) return;
    }

    public bool CompleteConnection(PortModel targetPort)
    {
        if (!IsDrawingConnection || ConnectionSourcePort == null)
        {
            CancelConnection();
            return false;
        }

        var action = CreateAction(UndoRedoActionType.ConnectionAdded, "建立连线");
        var result = _connectionService.PlanConnection(
            ConnectionSourcePort,
            targetPort,
            Nodes.Select(n => n.Model).ToArray(),
            Connections.ToArray());
        if (!result.Success)
        {
            Model.StatusText = result.Error ?? "无法建立连线";
            CancelConnection();
            return false;
        }

        foreach (var connection in result.Connections)
            Connections.Add(connection);

        action.ConnectionId = result.PrimaryConnection!.Id;
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

        CancelConnection();
        Model.IsModified = true;
        var status = result.CreatesCycle ? "已建立连线（注意：形成环路）" : "已建立连线";
        if (result.AutoBoundCount > 0)
            status += $"，自动绑定 {result.AutoBoundCount} 条数据连线";
        Model.StatusText = status;
        return true;
    }

    public void CancelConnection()
    {
        IsDrawingConnection = false;
        ConnectionSourcePort = null;
        PendingConnectionGeometry = null;
    }

    /// <summary>供视图预览候选端口，复用最终提交时的同一套连接规则。</summary>
    public bool CanCompleteConnection(PortModel targetPort)
        => IsDrawingConnection &&
           ConnectionSourcePort != null &&
           _connectionService.GetRejectionReason(
               ConnectionSourcePort,
               targetPort,
               Connections,
               Nodes.Select(node => node.Model)) == null;

    public void SetPendingGeometry(Geometry geometry)
    {
        PendingConnectionGeometry = geometry;
    }

    #endregion

    #region 缩放

    public event EventHandler<(double contentWidth, double contentHeight, double offsetX, double offsetY)>? ZoomToFitRequested
    {
        add => Service.ZoomToFitRequested += value;
        remove => Service.ZoomToFitRequested -= value;
    }

    #endregion

    #region 辅助

    /// <summary>追加日志到输出面板。</summary>
    public void AppendLog(string message) => Service.AppendLog(message);

    /// <summary>查找节点。</summary>
    public NodeViewModel? FindNode(string nodeId) => Service.FindNode(nodeId);

    /// <summary>重命名标签页标题（由 View 层双击标签编辑触发）。</summary>
    public void RenameTab(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) return;
        Model.TabTitle = newTitle;
        Model.IsModified = true;
    }

    /// <summary>更新命令可执行状态。</summary>
    public void NotifyCanExecuteChanged()
    {
        RunCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        LoopRunCommand.NotifyCanExecuteChanged();
        LoopStopCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        StepContinueCommand.NotifyCanExecuteChanged();
        ContinueFromNodeCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>根据节点位置自动调整画布尺寸。</summary>
    private void RecalculateCanvasSize()
    {
        if (Nodes.Count == 0)
        {
            Model.CanvasWidth = FlowChartModel.MinCanvasWidth;
            Model.CanvasHeight = FlowChartModel.MinCanvasHeight;
            return;
        }
        var maxX = Nodes.Max(n => n.X + GetNodeWidth(n)) +
            FlowChartModel.CanvasPadding;
        var maxY = Nodes.Max(n => n.Y + GetNodeHeight(n)) +
            FlowChartModel.CanvasPadding;
        Model.CanvasWidth = Math.Max(maxX, FlowChartModel.MinCanvasWidth);
        Model.CanvasHeight = Math.Max(maxY, FlowChartModel.MinCanvasHeight);
    }

    /// <summary>以 2 倍分辨率导出指定画布区域，背景透明。</summary>
    public void ExportPng(FrameworkElement? element, Rect contentBounds)
    {
        if (element != null)
            _exportService.ExportToPngWithDialog(element, contentBounds, 2.0);
    }

    /// <summary>PNG 应使用完整逻辑画布范围，与视口和滚动位置无关。</summary>
    public Rect GetCanvasExportBounds() =>
        new(0, 0, Model.CanvasWidth, Model.CanvasHeight);

    /// <summary>导出 SVG（由 View 层调用）。</summary>
    public void ExportSvg(FrameworkElement? element)
    {
        if (element != null) _exportService.ExportToSvgWithDialog(element);
    }

    #endregion
}
