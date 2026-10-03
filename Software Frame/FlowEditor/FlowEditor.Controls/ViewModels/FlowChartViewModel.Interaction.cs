// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Collections.ObjectModel、System.Windows、System.Windows.Media、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.ViewModels;

/// <summary>包含节点选择、批量拖动及临时连线表现状态。</summary>
public partial class FlowChartViewModel
{
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

    /// <summary>记录节点拖动起点及参与批量拖动的节点初始坐标。</summary>
    public void BeginNodeDrag(NodeViewModel node, Point mousePosition)
    {
        if (!node.IsSelected) SetSelectedNodes([node]);
        IEnumerable<NodeViewModel> selected = SelectedNodes.Count > 0 ? SelectedNodes : [node];
        _groupDragStart = mousePosition;
        _groupDragOrigins = _canvas.ResolveDragParticipants(node, selected)
            .ToDictionary(n => n, n => new Point(n.X, n.Y));
        node.BeginDrag(mousePosition);
    }

    /// <summary>根据鼠标位移更新当前拖动节点集合的位置。</summary>
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

    /// <summary>结束节点拖动并把位置变化写入撤销历史。</summary>
    public void EndNodeDrag(NodeViewModel node)
    {
        node.EndDrag();
        if (_groupDragOrigins != null)
        {
            foreach (var moved in _groupDragOrigins.Keys.ToList())
                _canvas.RefreshExecutionMembership(moved);
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

    private bool _isDrawingConnection;

    /// <summary>当前是否正在从输出端口绘制临时连线。</summary>
    public bool IsDrawingConnection
    {
        get => _isDrawingConnection;
        private set => SetProperty(ref _isDrawingConnection, value);
    }

    private PortModel? _connectionSourcePort;

    /// <summary>当前临时连线的源输出端口。</summary>
    public PortModel? ConnectionSourcePort
    {
        get => _connectionSourcePort;
        private set => SetProperty(ref _connectionSourcePort, value);
    }

    private Geometry? _pendingConnectionGeometry;

    /// <summary>视图计算出的临时连线几何图形。</summary>
    public Geometry? PendingConnectionGeometry
    {
        get => _pendingConnectionGeometry;
        private set => SetProperty(ref _pendingConnectionGeometry, value);
    }
}
