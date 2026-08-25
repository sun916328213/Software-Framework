using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;

namespace FlowEditor.ViewModels;

/// <summary>
/// 节点 ViewModel。
/// 持有 NodeModel 引用，管理拖拽、选中、端口状态等 UI 逻辑。
/// </summary>
public class NodeViewModel : ObservableObject, INodeExecutable
{
    /// <summary>底层节点模型。</summary>
    public NodeModel Model { get; }

    public NodeViewModel(NodeModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));

        // 将 Model 的属性变化转发到 ViewModel
        Model.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(e.PropertyName);
            if (e.PropertyName is nameof(NodeModel.X) or nameof(NodeModel.Y))
                OnPropertyChanged(nameof(Position));
        };
    }

    #region 绑定属性

    /// <summary>节点 ID。</summary>
    public string Id => Model.Id;

    /// <summary>节点名称。</summary>
    public string Name
    {
        get => Model.Name;
        set => Model.Name = value;
    }

    /// <summary>画布 X 坐标。</summary>
    public double X
    {
        get => Model.X;
        set => Model.X = value;
    }

    /// <summary>画布 Y 坐标。</summary>
    public double Y
    {
        get => Model.Y;
        set => Model.Y = value;
    }

    /// <summary>节点位置（便于 View 层整体绑定）。</summary>
    public Point Position => new(Model.X, Model.Y);

    /// <summary>输入端口集合。</summary>
    public IReadOnlyList<PortModel> InputPorts => Model.InputPorts;

    /// <summary>输出端口集合。</summary>
    public IReadOnlyList<PortModel> OutputPorts => Model.OutputPorts;

    /// <summary>最近一次执行耗时（毫秒），转发自 Model。</summary>
    public long LastElapsedMs
    {
        get => Model.LastElapsedMs;
        set => Model.LastElapsedMs = value;
    }

    public int Count
    {
        get => Model.Count;
        set => Model.Count = value;
    }

    /// <summary>节点执行状态。</summary>
    public NodeStatus Status
    {
        get => Model.Status;
        set => Model.Status = value;
    }

    /// <summary>节点权限。</summary>
    public NodePermission Permission
    {
        get => Model.Permission;
        set => Model.Permission = value;
    }

    /// <summary>是否可拖动。</summary>
    public bool IsDraggable => Model.IsDraggable;

    /// <summary>是否可编辑。</summary>
    public bool IsEditable => Model.IsEditable;

    /// <summary>底部操作按钮。</summary>
    public ObservableCollection<NodeActionButton> ActionButtons => Model.ActionButtons;

    private bool _isSelected;
    /// <summary>是否被选中（高亮显示）。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isDragging;
    /// <summary>是否正在拖拽中。</summary>
    public bool IsDragging
    {
        get => _isDragging;
        private set => SetProperty(ref _isDragging, value);
    }

    #endregion

    #region 拖拽逻辑

    private Point _dragStart;
    private double _dragStartX;
    private double _dragStartY;

    /// <summary>拖拽开始（鼠标在标题栏按下时调用）。</summary>
    public void BeginDrag(Point mousePosition)
    {
        if (!IsDraggable) return;
        _dragStart = mousePosition;
        _dragStartX = Model.X;
        _dragStartY = Model.Y;
        IsDragging = true;
    }

    /// <summary>拖拽移动（鼠标移动时调用）。</summary>
    public void UpdateDrag(Point mousePosition)
    {
        if (!IsDragging) return;
        var dx = mousePosition.X - _dragStart.X;
        var dy = mousePosition.Y - _dragStart.Y;
        Model.X = _dragStartX + dx;
        Model.Y = _dragStartY + dy;
    }

    /// <summary>拖拽结束（鼠标释放时调用）。</summary>
    public void EndDrag()
    {
        IsDragging = false;
    }

    #endregion

    #region 端口辅助

    /// <summary>根据端口 ID 查找端口。</summary>
    public PortModel? FindPort(string portId)
        => Model.FindPortById(portId);

    /// <summary>重置节点状态为 Idle。</summary>
    public void ResetStatus()
    {
        Model.Status = NodeStatus.Idle;
    }

    #endregion

    #region INodeExecutable 显式实现（转发至 Model）

    PortModel? INodeExecutable.FindPortById(string portId) => Model.FindPortById(portId);

    Task INodeExecutable.ExecuteAsync(CancellationToken ct) => Model.ExecuteAsync(ct);

    void INodeExecutable.SyncInputValues() => Model.SyncInputValues();

    void INodeExecutable.SyncOutputValues() => Model.SyncOutputValues();

    void INodeExecutable.ResetActivation() => ((INodeExecutable)Model).ResetActivation();

    IReadOnlyCollection<string>? INodeExecutable.GetActivatedOutputNames()
        => ((INodeExecutable)Model).GetActivatedOutputNames();

    IReadOnlyList<string>? INodeExecutable.ControlFlowPorts => Model.ControlFlowPorts;

    #endregion

    public override string ToString() => $"[{Name}]";
}