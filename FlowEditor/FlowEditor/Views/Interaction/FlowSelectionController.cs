using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using FlowEditor.Models;
using FlowEditor.ViewModels;

namespace FlowEditor.Views.Interaction;

/// <summary>
/// 管理节点点击、连线点击和拖框选择，并维护参与框选的节点视觉索引。
/// </summary>
internal sealed class FlowSelectionController
{
    private readonly Canvas _nodeCanvas;
    private readonly Canvas _connectionCanvas;
    private readonly Rectangle _selectionRectangle;
    private readonly Func<FlowChartViewModel?> _getViewModel;
    private readonly Func<bool> _isPanning;
    private readonly Action _cancelConnection;
    private readonly Action _requestFocus;
    private readonly Dictionary<NodeViewModel, FrameworkElement> _nodeViews = new();
    private readonly Dictionary<NodeViewModel, MouseButtonEventHandler> _nodeHandlers = new();
    private Point _boxSelectStart;
    private bool _isBoxSelecting;

    /// <summary>绑定选择所需的画布、选择框和宿主状态访问器。</summary>
    public FlowSelectionController(
        Canvas nodeCanvas,
        Canvas connectionCanvas,
        Rectangle selectionRectangle,
        Func<FlowChartViewModel?> getViewModel,
        Func<bool> isPanning,
        Action cancelConnection,
        Action requestFocus)
    {
        _nodeCanvas = nodeCanvas ?? throw new ArgumentNullException(nameof(nodeCanvas));
        _connectionCanvas = connectionCanvas ?? throw new ArgumentNullException(nameof(connectionCanvas));
        _selectionRectangle = selectionRectangle ?? throw new ArgumentNullException(nameof(selectionRectangle));
        _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
        _isPanning = isPanning ?? throw new ArgumentNullException(nameof(isPanning));
        _cancelConnection = cancelConnection ?? throw new ArgumentNullException(nameof(cancelConnection));
        _requestFocus = requestFocus ?? throw new ArgumentNullException(nameof(requestFocus));

        _nodeCanvas.MouseLeftButtonDown += OnCanvasMouseDown;
        _nodeCanvas.MouseLeftButtonUp += OnCanvasMouseUp;
        _nodeCanvas.MouseMove += OnCanvasMouseMove;
    }

    /// <summary>注册一个节点视觉对象，使其参与点击选择和拖框命中。</summary>
    public void RegisterNode(NodeViewModel node, FrameworkElement view)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(view);

        UnregisterNode(node);
        MouseButtonEventHandler handler = (_, e) => OnNodeMouseDown(node, e);
        view.MouseLeftButtonDown += handler;
        _nodeViews[node] = view;
        _nodeHandlers[node] = handler;
    }

    /// <summary>注销节点视觉对象及其点击处理器。</summary>
    public void UnregisterNode(NodeViewModel node)
    {
        if (_nodeViews.Remove(node, out var view) && _nodeHandlers.Remove(node, out var handler))
            view.MouseLeftButtonDown -= handler;
    }

    /// <summary>清空节点视觉索引并取消未完成的框选。</summary>
    public void ClearNodes()
    {
        foreach (var (node, view) in _nodeViews)
        {
            if (_nodeHandlers.TryGetValue(node, out var handler))
                view.MouseLeftButtonDown -= handler;
        }

        _nodeViews.Clear();
        _nodeHandlers.Clear();
        EndBoxSelection(updateSelection: false, default);
    }

    /// <summary>根据修饰键切换或替换单个节点选择。</summary>
    private void OnNodeMouseDown(NodeViewModel node, MouseButtonEventArgs e)
    {
        var viewModel = _getViewModel();
        if (viewModel == null ||
            PortConnectorLocator.FindAncestor(e.OriginalSource as DependencyObject) != null) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            viewModel.ToggleNodeSelection(node);
        else if (!node.IsSelected)
            viewModel.SetSelectedNodes([node]);
        e.Handled = true;
    }

    /// <summary>处理连线选中、空白处取消及框选开始。</summary>
    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        var viewModel = _getViewModel();
        if (viewModel == null || _isPanning() || e.ChangedButton != MouseButton.Left) return;
        _requestFocus();

        viewModel.SetSelectedNodes([]);
        viewModel.SelectedConnection = null;

        var point = e.GetPosition(_connectionCanvas);
        var hit = _connectionCanvas.InputHitTest(point) as FrameworkElement;
        if (hit?.Tag is ConnectionModel connection)
        {
            viewModel.SelectedConnection = connection;
            e.Handled = true;
            return;
        }

        if (e.Source != _nodeCanvas && e.Source is not Canvas) return;
        _cancelConnection();
        _boxSelectStart = e.GetPosition(_nodeCanvas);
        _isBoxSelecting = true;
        UpdateSelectionRectangle(new Rect(_boxSelectStart, _boxSelectStart));
        _selectionRectangle.Visibility = Visibility.Visible;
        _nodeCanvas.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>拖动框选时更新选择区域。</summary>
    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isBoxSelecting || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateBoxSelection(e.GetPosition(_nodeCanvas));
    }

    /// <summary>提交框选结果并释放鼠标捕获。</summary>
    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isBoxSelecting) return;
        EndBoxSelection(updateSelection: true, e.GetPosition(_nodeCanvas));
        e.Handled = true;
    }

    /// <summary>更新选择矩形并选中与其相交的节点。</summary>
    private void UpdateBoxSelection(Point current)
    {
        var viewModel = _getViewModel();
        if (viewModel == null) return;

        var rect = new Rect(_boxSelectStart, current);
        UpdateSelectionRectangle(rect);
        var selected = _nodeViews
            .Where(pair => rect.IntersectsWith(GetNodeBounds(pair.Key, pair.Value)))
            .Select(pair => pair.Key);
        viewModel.SetSelectedNodes(selected);
    }

    /// <summary>更新选择矩形的位置和尺寸。</summary>
    private void UpdateSelectionRectangle(Rect rect)
    {
        Canvas.SetLeft(_selectionRectangle, rect.Left);
        Canvas.SetTop(_selectionRectangle, rect.Top);
        _selectionRectangle.Width = rect.Width;
        _selectionRectangle.Height = rect.Height;
    }

    /// <summary>结束当前框选，可选择是否在结束前提交最终位置。</summary>
    private void EndBoxSelection(bool updateSelection, Point current)
    {
        if (updateSelection && _isBoxSelecting)
            UpdateBoxSelection(current);
        _isBoxSelecting = false;
        _selectionRectangle.Visibility = Visibility.Collapsed;
        if (_nodeCanvas.IsMouseCaptured)
            _nodeCanvas.ReleaseMouseCapture();
    }

    /// <summary>取得节点用于框选命中的画布边界。</summary>
    private static Rect GetNodeBounds(NodeViewModel node, FrameworkElement view) => new(
        node.X,
        node.Y,
        view.ActualWidth > 0 ? view.ActualWidth : FlowChartModel.NodeWidth,
        view.ActualHeight > 0 ? view.ActualHeight : FlowChartModel.NodeHeight);
}
