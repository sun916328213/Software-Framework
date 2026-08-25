using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using Prism.Ioc;
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
    private readonly IContainerProvider _containerProvider;

    // ── 命令属性 ──
    public AsyncRelayCommand RunCommand { get; private set; } = null!;
    public RelayCommand StopCommand { get; private set; } = null!;
    public AsyncRelayCommand LoopRunCommand { get; private set; } = null!;
    public RelayCommand LoopStopCommand { get; private set; } = null!;
    public RelayCommand SaveCommand { get; private set; } = null!;
    public RelayCommand SaveAsCommand { get; private set; } = null!;
    public RelayCommand LoadCommand { get; private set; } = null!;
    public RelayCommand ClearAllCommand { get; private set; } = null!;
    public RelayCommand NewFlowCommand { get; private set; } = null!;
    public RelayCommand ZoomInCommand { get; private set; } = null!;
    public RelayCommand ZoomOutCommand { get; private set; } = null!;
    public RelayCommand ZoomResetCommand { get; private set; } = null!;
    public RelayCommand ZoomToFitCommand { get; private set; } = null!;
    public RelayCommand DeleteSelectedCommand { get; private set; } = null!;
    public RelayCommand UndoCommand { get; private set; } = null!;
    public RelayCommand RedoCommand { get; private set; } = null!;
    public RelayCommand StepContinueCommand { get; private set; } = null!;
    public RelayCommand<FrameworkElement?> ExportPngCommand { get; private set; } = null!;
    public RelayCommand<FrameworkElement?> ExportSvgCommand { get; private set; } = null!;
    public RelayCommand CopyCommand { get; private set; } = null!;
    public RelayCommand PasteCommand { get; private set; } = null!;

    /// <summary>删除连线命令。</summary>
    public RelayCommand<ConnectionModel?> DeleteConnectionCommand { get; private set; } = null!;

    /// <summary>新增输入端口命令（节点右键菜单）。</summary>
    public RelayCommand<NodeViewModel> AddInputPortCommand { get; private set; } = null!;

    /// <summary>新增输出端口命令（节点右键菜单）。</summary>
    public RelayCommand<NodeViewModel> AddOutputPortCommand { get; private set; } = null!;

    /// <summary>复制节点命令。</summary>
    public RelayCommand<NodeViewModel> CopyNodeCommand { get; private set; } = null!;

    /// <summary>删除节点命令。</summary>
    public RelayCommand<NodeViewModel> DeleteNodeCommand { get; private set; } = null!;

    /// <summary>设置节点权限为正常。</summary>
    public RelayCommand<NodeViewModel> SetNormalPermissionCommand { get; private set; } = null!;

    /// <summary>设置节点权限为只读。</summary>
    public RelayCommand<NodeViewModel> SetReadOnlyPermissionCommand { get; private set; } = null!;

    /// <summary>设置节点权限为禁用。</summary>
    public RelayCommand<NodeViewModel> SetDisabledPermissionCommand { get; private set; } = null!;

    /// <summary>流程图运行时模型。View 直接绑定到此模型属性。</summary>
    public FlowChartModel Model => Service.Model;

    /// <summary>画布上所有节点。</summary>
    public ObservableCollection<NodeViewModel> Nodes => Service.Nodes;

    /// <summary>所有连线。</summary>
    public ObservableCollection<ConnectionModel> Connections => Service.Connections;

    /// <summary>撤销/重做服务。</summary>
    public IUndoRedoService UndoRedo => Service.UndoRedo;

    #region 构造函数

    public FlowChartViewModel(
        IFlowChartService service,
        IExportService exportService,
        IDialogService dialogService,
        IContainerProvider containerProvider)
    {
        Service = service;
        _exportService = exportService;
        _dialogService = dialogService;
        _containerProvider = containerProvider;
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

    private void InitializeCommands()
    {
        RunCommand = new AsyncRelayCommand(RunAsync, CanRun);
        StopCommand = new RelayCommand(Stop, CanStop);
        LoopRunCommand = new AsyncRelayCommand(LoopRunAsync, CanLoopRun);
        LoopStopCommand = new RelayCommand(LoopStop, CanLoopStop);
        SaveCommand = new RelayCommand(Save);
        SaveAsCommand = new RelayCommand(SaveAs);
        LoadCommand = new RelayCommand(Load);
        ClearAllCommand = new RelayCommand(ClearAll);
        NewFlowCommand = new RelayCommand(NewFlow);
        ZoomInCommand = new RelayCommand(Service.ZoomIn);
        ZoomOutCommand = new RelayCommand(Service.ZoomOut);
        ZoomResetCommand = new RelayCommand(Service.ZoomReset);
        ZoomToFitCommand = new RelayCommand(Service.ZoomToFit);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected);
        UndoCommand = new RelayCommand(Undo, () => Service.UndoRedo.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => Service.UndoRedo.CanRedo);
        StepContinueCommand = new RelayCommand(Service.StepContinue);
        ExportPngCommand = new RelayCommand<FrameworkElement?>(ExportPng);
        ExportSvgCommand = new RelayCommand<FrameworkElement?>(ExportSvg);
        CopyCommand = new RelayCommand(CopySelected);
        PasteCommand = new RelayCommand(PasteAtCenter);
        DeleteConnectionCommand = new RelayCommand<ConnectionModel?>(DeleteConnection);

        // 节点右键菜单命令
        AddInputPortCommand = new RelayCommand<NodeViewModel>(AddInputPort);
        AddOutputPortCommand = new RelayCommand<NodeViewModel>(AddOutputPort);
        CopyNodeCommand = new RelayCommand<NodeViewModel>(node => { SelectedNode = node; CopySelected(); });
        DeleteNodeCommand = new RelayCommand<NodeViewModel>(node => { SelectedNode = node; DeleteSelected(); });
        SetNormalPermissionCommand = new RelayCommand<NodeViewModel>(node => SetPermission(node, NodePermission.Normal));
        SetReadOnlyPermissionCommand = new RelayCommand<NodeViewModel>(node => SetPermission(node, NodePermission.ReadOnly));
        SetDisabledPermissionCommand = new RelayCommand<NodeViewModel>(node => SetPermission(node, NodePermission.Disabled));
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
            if (SetProperty(ref _selectedNode, value))
            {
                foreach (var n in Nodes)
                    n.IsSelected = n == value;
            }
        }
    }

    /// <summary>多选节点集合（框选时使用）。</summary>
    public ObservableCollection<NodeViewModel> SelectedNodes { get; } = new();

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

    private NodeModel? _clipboardNode;

    /// <summary>复制选中的节点。</summary>
    private void CopySelected()
    {
        if (SelectedNode == null) return;
        _clipboardNode = SelectedNode.Model;
        Model.StatusText = "已复制节点";
    }

    /// <summary>粘贴节点到画布中心。</summary>
    private void PasteAtCenter()
    {
        if (_clipboardNode == null) return;
        var newNode = RebuildNodeFromClipboard(_clipboardNode);
        newNode.X += 50;
        newNode.Y += 50;

        var action = CreateAction(UndoRedoActionType.NodeAdded, $"粘贴节点: {newNode.Name}", nodeId: newNode.Id);
        Service.AddNode(newNode);
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

        Model.IsModified = true;
        Model.StatusText = "已粘贴节点";
        RecalculateCanvasSize();
    }

    private NodeModel RebuildNodeFromClipboard(NodeModel source)
    {
        var type = source.GetType();
        var newNode = _containerProvider.Resolve(type) as NodeModel
                      ?? (NodeModel)Activator.CreateInstance(type)!;
        newNode.Name = source.Name + "_副本";
        newNode.IsInEnabled = source.IsInEnabled;
        newNode.IsOutEnabled = source.IsOutEnabled;
        newNode.X = source.X;
        newNode.Y = source.Y;
        foreach (var port in source.CustomInputPorts)
            newNode.AddCustomInputPort(port.Name);
        foreach (var port in source.CustomOutputPorts)
            newNode.AddCustomOutputPort(port.Name, port.PropertyName);
        return newNode;
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
        Service.AddNode(node);
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
        Service.AddNode(node);
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
        if (SelectedNode == null) return;
        var nodeId = SelectedNode.Id;
        var nodeName = SelectedNode.Name;

        var action = CreateAction(UndoRedoActionType.NodeDeleted, $"删除节点: {nodeName}", nodeId: nodeId);
        var toRemove = Connections.Where(c =>
            c.SourceNodeId == nodeId || c.TargetNodeId == nodeId).ToList();
        foreach (var conn in toRemove)
            Connections.Remove(conn);
        Service.RemoveNode(SelectedNode);
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

        SelectedNode = null;
        Model.IsModified = true;
        Model.StatusText = "已删除节点";
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

    #endregion

    #region 执行命令

    private async Task RunAsync()
    {
        await Service.RunAsync(msg => AppendLog(msg), debugMode: false);
    }

    private bool CanRun() => !Model.IsRunning && Nodes.Count > 0;
    private void Stop() => Service.Stop();
    private bool CanStop() => Model.IsRunning;

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

        if (ConnectionSourcePort.OwnerNodeId == targetPort.OwnerNodeId)
        {
            Model.StatusText = "不能连接同一节点的端口";
            CancelConnection();
            return false;
        }

        if (ConnectionSourcePort.Direction != PortDirection.Output ||
            targetPort.Direction != PortDirection.Input)
        {
            Model.StatusText = "只能从输出端口连到输入端口";
            CancelConnection();
            return false;
        }

        // 检查是否存在完全相同的连线（同一源端口到同一目标端口）
        if (Connections.Any(c => c.SourcePortId == ConnectionSourcePort.Id &&
                                  c.TargetPortId == targetPort.Id))
        {
            Model.StatusText = "这两个端口之间已存在连线";
            CancelConnection();
            return false;
        }

        // 数据类型输入端口只允许单连接（Flow 类型允许多连接）
        if (targetPort.DataType != PortType.Flow)
        {
            var existingIncoming = Connections.Count(c => c.TargetPortId == targetPort.Id);
            if (existingIncoming > 0)
            {
                Model.StatusText = $"数据端口「{targetPort.Name}」已连接，仅允许一条连线";
                CancelConnection();
                return false;
            }
        }

        // 检测环路：如果 targetNode 已经存在到 sourceNode 的路径，
        // 则添加此连线会形成环路，仍然允许但给出警告
        bool createsCycle = WouldCreateCycle(ConnectionSourcePort.OwnerNodeId, targetPort.OwnerNodeId);

        var sourceNodeId = ConnectionSourcePort.OwnerNodeId;
        var sourcePortId = ConnectionSourcePort.Id;

        var action = CreateAction(UndoRedoActionType.ConnectionAdded, "建立连线");
        var conn = new ConnectionModel
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetPort.OwnerNodeId,
            TargetPortId = targetPort.Id
        };

        Connections.Add(conn);

        // Flow 连线创建后自动绑定匹配的数据端口
        int autoBound = 0;
        if (ConnectionSourcePort.DataType == PortType.Flow)
            autoBound = AutoBindDataPorts(sourceNodeId, targetPort.OwnerNodeId);

        action.ConnectionId = conn.Id;
        action.AfterData = Service.CaptureSnapshot();
        Service.UndoRedo.Push(action);

        CancelConnection();
        Model.IsModified = true;
        var status = createsCycle ? "已建立连线（注意：形成环路）" : "已建立连线";
        if (autoBound > 0)
            status += $"，自动绑定 {autoBound} 条数据连线";
        Model.StatusText = status;
        return true;
    }

    public void CancelConnection()
    {
        IsDrawingConnection = false;
        ConnectionSourcePort = null;
        PendingConnectionGeometry = null;
    }

    /// <summary>
    /// 检测从 sourceNodeId 添加一条到 targetNodeId 的连线是否会形成环路。
    /// 使用 BFS 从 targetNodeId 出发，检查是否能到达 sourceNodeId。
    /// </summary>
    private bool WouldCreateCycle(string sourceNodeId, string targetNodeId)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(targetNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == sourceNodeId)
                return true;
            if (!visited.Add(current))
                continue;

            foreach (var conn in Connections.Where(c => c.SourceNodeId == current))
            {
                if (!visited.Contains(conn.TargetNodeId))
                    queue.Enqueue(conn.TargetNodeId);
            }
        }
        return false;
    }

    /// <summary>
    /// 自动绑定两个节点之间 PropertyName 匹配的数据端口（非 Flow 类型）。
    /// 在 Flow 连线创建后调用，自动为匹配的输出→输入数据端口创建连线。
    /// </summary>
    private int AutoBindDataPorts(string sourceNodeId, string targetNodeId)
    {
        var sourceNode = FindNode(sourceNodeId);
        var targetNode = FindNode(targetNodeId);
        if (sourceNode == null || targetNode == null) return 0;

        int count = 0;
        foreach (var outPort in sourceNode.OutputPorts)
        {
            if (outPort.DataType == PortType.Flow) continue;
            var propName = outPort.PropertyName ?? outPort.Name;
            if (string.IsNullOrEmpty(propName)) continue;

            // 在目标节点的输入端口中找 PropertyName 匹配的数据端口
            var inPort = targetNode.InputPorts.FirstOrDefault(p =>
                p.DataType != PortType.Flow &&
                (p.PropertyName == propName || p.Name == propName));
            if (inPort == null) continue;

            // 已存在连线则跳过
            if (Connections.Any(c => c.SourcePortId == outPort.Id && c.TargetPortId == inPort.Id))
                continue;

            // 数据输入端口已连接其他端口则跳过（单连接约束）
            if (Connections.Any(c => c.TargetPortId == inPort.Id))
                continue;

            var dataConn = new ConnectionModel
            {
                SourceNodeId = sourceNodeId,
                SourcePortId = outPort.Id,
                TargetNodeId = targetNodeId,
                TargetPortId = inPort.Id
            };
            Connections.Add(dataConn);
            count++;
        }
        return count;
    }

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
        var maxX = Nodes.Max(n => n.X) + FlowChartModel.NodeWidth + FlowChartModel.CanvasPadding;
        var maxY = Nodes.Max(n => n.Y) + FlowChartModel.NodeHeight + FlowChartModel.CanvasPadding;
        Model.CanvasWidth = Math.Max(maxX, FlowChartModel.MinCanvasWidth);
        Model.CanvasHeight = Math.Max(maxY, FlowChartModel.MinCanvasHeight);
    }

    /// <summary>导出 PNG（由 View 层调用）。</summary>
    public void ExportPng(FrameworkElement? element)
    {
        if (element != null) _exportService.ExportToPngWithDialog(element);
    }

    /// <summary>导出 SVG（由 View 层调用）。</summary>
    public void ExportSvg(FrameworkElement? element)
    {
        if (element != null) _exportService.ExportToSvgWithDialog(element);
    }

    #endregion
}
