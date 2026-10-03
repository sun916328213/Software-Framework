// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowConnectionDragController。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Views、System.ComponentModel、System.Windows、System.Windows.Controls、System.Windows.Input。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Controls.Views.Interaction;

/// <summary>
/// 管理临时连线的绘制、候选端口吸附、鼠标完成事件及端口坐标缓存。
/// 模块直接订阅画布和当前 ViewModel，避免宿主视图参与连线手势生命周期。
/// </summary>
internal sealed class FlowConnectionDragController
{
    private const double PortSnapRadius = 28;
    private readonly Canvas _nodeCanvas;
    private readonly Canvas _pendingCanvas;
    private FlowChartViewModel? _viewModel;
    private PortConnectorView? _connectionCandidate;
    private Point _connectionCandidatePoint;
    private Path? _pendingConnectionPath;
    private PortModel? _cachedConnectionSource;
    private Point _cachedConnectionSourcePoint;
    private readonly List<(PortConnectorView Connector, Point Center)> _cachedConnectionTargets = [];

    /// <summary>创建控制器并接管画布的移动和鼠标释放事件。</summary>
    public FlowConnectionDragController(Canvas nodeCanvas, Canvas pendingCanvas)
    {
        _nodeCanvas = nodeCanvas ?? throw new ArgumentNullException(nameof(nodeCanvas));
        _pendingCanvas = pendingCanvas ?? throw new ArgumentNullException(nameof(pendingCanvas));
        _nodeCanvas.AddHandler(
            Mouse.MouseMoveEvent,
            new MouseEventHandler(OnMouseMove),
            handledEventsToo: true);
        _nodeCanvas.AddHandler(
            Mouse.MouseUpEvent,
            new MouseButtonEventHandler(OnMouseUp),
            handledEventsToo: true);
    }

    /// <summary>切换当前流程上下文并维护临时连线状态订阅。</summary>
    public void Attach(FlowChartViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        ResetVisualState();
        _viewModel = viewModel;
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>取消当前连线手势并清理候选高亮与鼠标捕获。</summary>
    public void Cancel()
    {
        _viewModel?.CancelConnection();
        ResetVisualState();
        Mouse.Capture(null);
    }

    /// <summary>节点视觉树变化后使端口坐标缓存失效。</summary>
    public void InvalidateLayout()
    {
        ClearConnectionCandidate();
        _cachedConnectionSource = null;
        _cachedConnectionTargets.Clear();
    }

    /// <summary>同步 ViewModel 的临时几何图形和连线手势状态。</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;
        if (e.PropertyName == nameof(FlowChartViewModel.PendingConnectionGeometry))
        {
            var geometry = _viewModel.PendingConnectionGeometry;
            if (geometry == null)
            {
                RemovePendingPath();
                return;
            }

            _pendingConnectionPath ??= CreatePendingConnectionPath();
            _pendingConnectionPath.Data = geometry;
            if (!_pendingCanvas.Children.Contains(_pendingConnectionPath))
                _pendingCanvas.Children.Add(_pendingConnectionPath);
        }
        else if (e.PropertyName is nameof(FlowChartViewModel.IsDrawingConnection)
                 or nameof(FlowChartViewModel.ConnectionSourcePort))
        {
            if (!_viewModel.IsDrawingConnection || _viewModel.ConnectionSourcePort == null)
                ResetVisualState();
            else
                InvalidateLayout();
        }
    }

    /// <summary>拖动连线时更新候选端口和临时贝塞尔曲线。</summary>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel?.IsDrawingConnection != true) return;
        var position = e.GetPosition(_nodeCanvas);
        EnsureConnectionDragCache();
        UpdateConnectionCandidate(position);

        var endPoint = _connectionCandidate == null ? position : _connectionCandidatePoint;
        _viewModel.SetPendingGeometry(
            ConnectionRenderer.BuildBezierGeometry(_cachedConnectionSourcePoint, endPoint));
    }

    /// <summary>在候选端口提交临时连线，否则取消本次连线。</summary>
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.IsDrawingConnection != true) return;

        var target = _connectionCandidate ??
            PortConnectorLocator.FindAncestor(e.OriginalSource as DependencyObject);
        if (target?.Port is PortModel targetPort)
        {
            _viewModel.CompleteConnection(targetPort);
            ResetVisualState();
            Mouse.Capture(null);
            e.Handled = true;
            return;
        }

        Cancel();
    }

    /// <summary>创建使用统一资源样式的临时连线路径。</summary>
    private Path CreatePendingConnectionPath()
    {
        var path = new Path();
        path.SetResourceReference(FrameworkElement.StyleProperty, "ConnectionPathStyle_Pending");
        return path;
    }

    /// <summary>为当前源端口缓存坐标及全部可连接的目标端口。</summary>
    private void EnsureConnectionDragCache()
    {
        var source = _viewModel?.ConnectionSourcePort;
        if (source == null || ReferenceEquals(source, _cachedConnectionSource)) return;

        _cachedConnectionTargets.Clear();
        _cachedConnectionSource = source;
        _cachedConnectionSourcePoint = new Point(0, 0);

        foreach (var connector in PortConnectorLocator.Enumerate(_nodeCanvas))
        {
            if (connector.Port is not { } port) continue;
            Point center;
            try { center = PortConnectorLocator.GetCenter(connector, _nodeCanvas); }
            catch (InvalidOperationException) { continue; }

            if (port.Id == source.Id)
                _cachedConnectionSourcePoint = center;
            else if (_viewModel!.CanCompleteConnection(port))
                _cachedConnectionTargets.Add((connector, center));
        }
    }

    /// <summary>选择吸附半径内最近的可连接端口并更新高亮。</summary>
    private void UpdateConnectionCandidate(Point mousePosition)
    {
        PortConnectorView? nearest = null;
        var nearestPoint = default(Point);
        var nearestDistance = PortSnapRadius;

        foreach (var (connector, center) in _cachedConnectionTargets)
        {
            var distance = (center - mousePosition).Length;
            if (distance > nearestDistance) continue;
            nearest = connector;
            nearestPoint = center;
            nearestDistance = distance;
        }

        if (ReferenceEquals(nearest, _connectionCandidate)) return;
        ClearConnectionCandidate();
        _connectionCandidate = nearest;
        _connectionCandidatePoint = nearestPoint;
        if (_connectionCandidate != null)
            _connectionCandidate.IsConnectionCandidate = true;
    }

    /// <summary>清除当前连接候选及其高亮状态。</summary>
    private void ClearConnectionCandidate()
    {
        if (_connectionCandidate != null)
            _connectionCandidate.IsConnectionCandidate = false;
        _connectionCandidate = null;
    }

    /// <summary>从临时画布移除当前连线路径。</summary>
    private void RemovePendingPath()
    {
        if (_pendingConnectionPath != null)
            _pendingCanvas.Children.Remove(_pendingConnectionPath);
    }

    /// <summary>清除临时路径、候选端口和全部拖动缓存。</summary>
    private void ResetVisualState()
    {
        RemovePendingPath();
        ClearConnectionCandidate();
        _cachedConnectionSource = null;
        _cachedConnectionTargets.Clear();
    }
}
