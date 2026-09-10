using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;

namespace FlowEditor.Views;

/// <summary>
/// 流程图画布视图（纯画布，不含工具箱/属性/输出/工具栏）。
/// 由 FlowChartContainerView 以标签页方式托管。
/// </summary>
public partial class FlowChartView : UserControl
{
    private FlowChartViewModel? _currentViewModel;
    private FlowChartViewModel ViewModel => _currentViewModel!;

    private readonly Dictionary<NodeViewModel, FrameworkElement> _nodeViewCache = new();
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodeSubscriptions = new();
    private Point _boxSelectStart;
    private bool _isBoxSelecting;
    private bool _isPanning;
    private MouseButton? _panButton;
    private Point _panStart;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private PortConnectorView? _connectionCandidate;
    private Point _connectionCandidatePoint;
    private Path? _pendingConnectionPath;
    private PortModel? _cachedConnectionSource;
    private Point _cachedConnectionSourcePoint;
    private readonly List<(PortConnectorView Connector, Point Center)> _cachedConnectionTargets = [];
    private const double PortSnapRadius = 28;

    /// <summary>暴露画布容器供容器层导出 PNG 等操作使用。</summary>
    public Grid ZoomContainerElement => ZoomContainer;

    public FlowChartView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        PreviewKeyDown += OnFlowChartKeyDown;
        PreviewMouseDown += (s, e) => Focus(); // 确保键盘焦点在画布上，Delete 键才有效
        FlowChartCanvas.AddHandler(
            Mouse.MouseUpEvent,
            new MouseButtonEventHandler(OnCanvasConnectionMouseUp),
            handledEventsToo: true);
    }

    #region ViewModel 绑定与切换

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 取消旧 ViewModel 及 Model 订阅
        if (_currentViewModel != null)
        {
            _currentViewModel.Nodes.CollectionChanged -= OnNodesCollectionChanged;
            _currentViewModel.Connections.CollectionChanged -= OnConnectionsCollectionChanged;
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _currentViewModel.Model.PropertyChanged -= OnModelPropertyChanged;
            _currentViewModel.ZoomToFitRequested -= OnZoomToFitRequested;
        }
        ClearNodeViews();

        // 订阅新 ViewModel
        _currentViewModel = DataContext as FlowChartViewModel;
        if (_currentViewModel == null) return;

        _currentViewModel.Nodes.CollectionChanged += OnNodesCollectionChanged;
        _currentViewModel.Connections.CollectionChanged += OnConnectionsCollectionChanged;
        _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _currentViewModel.Model.PropertyChanged += OnModelPropertyChanged;
        _currentViewModel.ZoomToFitRequested += OnZoomToFitRequested;

        RenderAllNodes();
    }

    private void OnConnectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScheduleConnectionRender();

    #endregion

    #region 节点渲染

    private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (NodeViewModel vm in e.NewItems)
                AddNodeViewToCanvas(vm);
        }
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (NodeViewModel vm in e.OldItems)
                RemoveNodeViewFromCanvas(vm);
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
            ClearNodeViews();
        ScheduleConnectionRender();
    }

    private void RenderAllNodes()
    {
        ClearNodeViews();
        foreach (var vm in ViewModel.Nodes)
            AddNodeViewToCanvas(vm);
        ScheduleConnectionRender();
    }

    private void AddNodeViewToCanvas(NodeViewModel vm)
    {
        FrameworkElement nodeView = vm.Model switch
        {
            InactiveNodeContainerNode => new InactiveNodeContainerView { DataContext = vm },
            IExecutionScopeContainer => new ExecutionContainerView { DataContext = vm },
            _ => new NodeView { DataContext = vm }
        };
        Canvas.SetLeft(nodeView, vm.X);
        Canvas.SetTop(nodeView, vm.Y);
        Panel.SetZIndex(nodeView, vm.Model switch
        {
            InactiveNodeContainerNode => -10,
            IExecutionScopeContainer => -5,
            _ => 0
        });

        PropertyChangedEventHandler propertyChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(NodeViewModel.X))
                Canvas.SetLeft(nodeView, vm.X);
            else if (e.PropertyName == nameof(NodeViewModel.Y))
                Canvas.SetTop(nodeView, vm.Y);
            if (e.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y))
                ScheduleConnectionRender();
        };
        vm.PropertyChanged += propertyChanged;

        nodeView.MouseLeftButtonDown += (_, e) =>
        {
            if (PortConnectorLocator.FindAncestor(e.OriginalSource as DependencyObject) != null)
                return;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                ViewModel.ToggleNodeSelection(vm);
            else if (!vm.IsSelected)
                ViewModel.SetSelectedNodes([vm]);
            e.Handled = true; // 阻止冒泡到画布，否则 OnCanvasMouseDown 会清空选中
        };

        FlowChartCanvas.Children.Add(nodeView);
        _nodeViewCache[vm] = nodeView;
        _nodeSubscriptions[vm] = propertyChanged;
    }

    private void RemoveNodeViewFromCanvas(NodeViewModel vm)
    {
        if (_nodeViewCache.TryGetValue(vm, out var nodeView))
        {
            FlowChartCanvas.Children.Remove(nodeView);
            _nodeViewCache.Remove(vm);
        }
        if (_nodeSubscriptions.Remove(vm, out var propertyChanged))
            vm.PropertyChanged -= propertyChanged;
    }

    private void ClearNodeViews()
    {
        foreach (var (node, handler) in _nodeSubscriptions)
            node.PropertyChanged -= handler;
        _nodeSubscriptions.Clear();
        _nodeViewCache.Clear();
        FlowChartCanvas.Children.Clear();
        ResetConnectionDragCache();
    }

    #endregion

    #region 连线渲染

    private bool _connectionRenderScheduled;

    private void ScheduleConnectionRender()
    {
        if (_connectionRenderScheduled) return;
        _connectionRenderScheduled = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            _connectionRenderScheduled = false;

            var allConnections = ViewModel.Connections.ToList();
            var nodes = ViewModel.Nodes.ToList();

            // 根据 ShowDataConnections 过滤连线
            var connectionsToRender = allConnections;
            if (!ViewModel.Model.ShowDataConnections)
            {
                var nodeMap = nodes.ToDictionary(n => n.Id, n => n);
                connectionsToRender = allConnections.Where(conn =>
                {
                    if (!nodeMap.TryGetValue(conn.SourceNodeId, out var srcNode)) return false;
                    var srcPort = srcNode.FindPort(conn.SourcePortId);
                    return srcPort?.DataType == PortType.Flow;
                }).ToList();
            }

            ConnectionRenderer.RenderConnections(
                ConnectionCanvas,
                FlowChartCanvas,
                connectionsToRender,
                nodes,
                ViewModel.SelectedConnection?.Id);
        }));
    }

    #endregion

    #region 临时连线

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowChartViewModel.PendingConnectionGeometry))
        {
            var geometry = ViewModel.PendingConnectionGeometry;
            if (geometry == null)
            {
                if (_pendingConnectionPath != null)
                    PendingCanvas.Children.Remove(_pendingConnectionPath);
                return;
            }

            _pendingConnectionPath ??= CreatePendingConnectionPath();
            _pendingConnectionPath.Data = geometry;
            if (!PendingCanvas.Children.Contains(_pendingConnectionPath))
                PendingCanvas.Children.Add(_pendingConnectionPath);
        }
        else if (e.PropertyName is nameof(FlowChartViewModel.IsDrawingConnection)
                 or nameof(FlowChartViewModel.ConnectionSourcePort))
        {
            if (!ViewModel.IsDrawingConnection || ViewModel.ConnectionSourcePort == null)
                ResetConnectionDragCache();
            else
                InvalidateConnectionDragCache();
        }
        else if (e.PropertyName == nameof(FlowChartViewModel.SelectedConnection))
        {
            ScheduleConnectionRender();
        }
        else if (e.PropertyName == nameof(FlowChartModel.ShowDataConnections))
        {
            ScheduleConnectionRender();
        }
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowChartModel.ShowDataConnections))
        {
            ScheduleConnectionRender();
        }
    }

    #endregion

    #region 画布鼠标事件

    private void OnCanvasDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(NodePaletteItem))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCanvasDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(NodePaletteItem)) is not NodePaletteItem item) return;
        ViewModel.AddNodeAt(item, e.GetPosition(ZoomContainer));
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning || e.ChangedButton != MouseButton.Left) return;
        Focus(); // 确保键盘焦点在 UserControl 上，否则 Delete 键无效

        ViewModel.SetSelectedNodes([]);
        ViewModel.SelectedConnection = null;

        var point = e.GetPosition(ConnectionCanvas);
        var hit = ConnectionCanvas.InputHitTest(point) as FrameworkElement;
        if (hit?.Tag is ConnectionModel conn)
        {
            ViewModel.SelectedConnection = ViewModel.SelectedConnection?.Id == conn.Id ? null : conn;
            e.Handled = true;
            return;
        }

        if (e.Source == FlowChartCanvas || e.Source is Canvas)
        {
            ViewModel.CancelConnection();
            ClearConnectionCandidate();
            _boxSelectStart = e.GetPosition(FlowChartCanvas);
            _isBoxSelecting = true;
            Canvas.SetLeft(SelectionRectangle, _boxSelectStart.X);
            Canvas.SetTop(SelectionRectangle, _boxSelectStart.Y);
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            SelectionRectangle.Visibility = Visibility.Visible;
            FlowChartCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnCanvasConnectionMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.IsDrawingConnection) return;

        var target = _connectionCandidate ??
            PortConnectorLocator.FindAncestor(e.OriginalSource as DependencyObject);
        if (target?.Port is PortModel targetPort)
        {
            ViewModel.CompleteConnection(targetPort);
            ClearConnectionCandidate();
            Mouse.Capture(null);
            e.Handled = true;
            return;
        }

        ViewModel.CancelConnection();
        ClearConnectionCandidate();
        Mouse.Capture(null);
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_currentViewModel == null) return;
        if (_isBoxSelecting && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateBoxSelection(e.GetPosition(FlowChartCanvas));
            return;
        }
        if (ViewModel.IsDrawingConnection)
        {
            var pos = e.GetPosition(FlowChartCanvas);
            EnsureConnectionDragCache();
            UpdateConnectionCandidate(pos);

            var endPoint = _connectionCandidate == null ? pos : _connectionCandidatePoint;
            var geometry = ConnectionRenderer.BuildBezierGeometry(_cachedConnectionSourcePoint, endPoint);
            ViewModel.SetPendingGeometry(geometry);
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isBoxSelecting) return;
        UpdateBoxSelection(e.GetPosition(FlowChartCanvas));
        _isBoxSelecting = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        FlowChartCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateBoxSelection(Point current)
    {
        var rect = new Rect(_boxSelectStart, current);
        Canvas.SetLeft(SelectionRectangle, rect.Left);
        Canvas.SetTop(SelectionRectangle, rect.Top);
        SelectionRectangle.Width = rect.Width;
        SelectionRectangle.Height = rect.Height;

        var selected = _nodeViewCache
            .Where(pair =>
            {
                var nodeRect = new Rect(
                    pair.Key.X,
                    pair.Key.Y,
                    pair.Value.ActualWidth > 0 ? pair.Value.ActualWidth : FlowChartModel.NodeWidth,
                    pair.Value.ActualHeight > 0 ? pair.Value.ActualHeight : FlowChartModel.NodeHeight);
                return rect.IntersectsWith(nodeRect);
            })
            .Select(pair => pair.Key);
        ViewModel.SetSelectedNodes(selected);
    }

    private void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(FlowChartCanvas);
        var hitResult = VisualTreeHelper.HitTest(FlowChartCanvas, pos);
        var hitNode = FindAncestorNodeView(hitResult?.VisualHit);

        if (hitNode?.DataContext is NodeViewModel nodeVm)
        {
            ViewModel.SelectedNode = nodeVm;
            ShowNodeContextMenu(nodeVm);
            e.Handled = true;
            return;
        }

        ShowCanvasContextMenu();
        ViewModel.CancelConnection();
        ClearConnectionCandidate();
    }

    private void ShowNodeContextMenu(NodeViewModel nodeVm)
    {
        var menu = (ContextMenu)FindResource("NodeContextMenu");
        menu.DataContext = new NodeContextMenuContext(ViewModel, nodeVm);
        menu.IsOpen = true;
    }

    private void ShowCanvasContextMenu()
    {
        var menu = (ContextMenu)FindResource("CanvasContextMenu");
        menu.DataContext = ViewModel;
        menu.IsOpen = true;
    }

    private static NodeView? FindAncestorNodeView(DependencyObject? element)
    {
        var current = element;
        while (current != null)
        {
            if (current is NodeView nodeView) return nodeView;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void OnFlowChartKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.C)
        {
            ViewModel.CopyCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.V)
        {
            ViewModel.PasteCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Delete)
        {
            if (ViewModel.SelectedConnection != null)
            {
                ViewModel.DeleteConnectionCommand.Execute(ViewModel.SelectedConnection);
                e.Handled = true;
            }
            else if (ViewModel.SelectedNodes.Count > 0 || ViewModel.SelectedNode != null)
            {
                ViewModel.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    #endregion

    #region 缩放控制

    internal static bool ShouldStartCanvasPan(MouseButton button, ModifierKeys modifiers) =>
        button == MouseButton.Middle ||
        (button == MouseButton.Left && modifiers.HasFlag(ModifierKeys.Shift));

    private void OnPanMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentViewModel == null ||
            !ShouldStartCanvasPan(e.ChangedButton, Keyboard.Modifiers)) return;

        _isPanning = true;
        _panButton = e.ChangedButton;
        _panStart = e.GetPosition(ScrollViewerHost);
        _panStartHorizontalOffset = ScrollViewerHost.HorizontalOffset;
        _panStartVerticalOffset = ScrollViewerHost.VerticalOffset;
        ZoomContainer.Cursor = Cursors.Hand;
        ZoomContainer.CaptureMouse();
        e.Handled = true;
    }

    private void OnPanMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        var panButtonPressed = _panButton switch
        {
            MouseButton.Middle => e.MiddleButton == MouseButtonState.Pressed,
            MouseButton.Left => e.LeftButton == MouseButtonState.Pressed,
            _ => false
        };
        if (!panButtonPressed) return;
        var current = e.GetPosition(ScrollViewerHost);
        var delta = current - _panStart;
        ScrollViewerHost.ScrollToHorizontalOffset(_panStartHorizontalOffset - delta.X);
        ScrollViewerHost.ScrollToVerticalOffset(_panStartVerticalOffset - delta.Y);
        e.Handled = true;
    }

    private void OnPanMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning || e.ChangedButton != _panButton) return;
        _isPanning = false;
        _panButton = null;
        ZoomContainer.ReleaseMouseCapture();
        ZoomContainer.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var containerPos = e.GetPosition(ZoomContainer);
        var mousePos = e.GetPosition(ScrollViewerHost);
        double oldZoom = ViewModel.Model.ZoomLevel;

        if (e.Delta > 0)
            ViewModel.ZoomInCommand.Execute(null);
        else
            ViewModel.ZoomOutCommand.Execute(null);

        double newZoom = ViewModel.Model.ZoomLevel;
        if (Math.Abs(newZoom - oldZoom) < 0.001) return;

        double newOffsetX = containerPos.X * newZoom - mousePos.X;
        double newOffsetY = containerPos.Y * newZoom - mousePos.Y;
        ScrollViewerHost.ScrollToHorizontalOffset(newOffsetX);
        ScrollViewerHost.ScrollToVerticalOffset(newOffsetY);
    }

    private void OnZoomToFitRequested(object? sender,
        (double contentWidth, double contentHeight, double offsetX, double offsetY) data)
    {
        double viewportWidth = ScrollViewerHost.ViewportWidth;
        double viewportHeight = ScrollViewerHost.ViewportHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        double scaleX = viewportWidth / data.contentWidth;
        double scaleY = viewportHeight / data.contentHeight;
        double scale = Math.Min(scaleX, scaleY);
        scale = Math.Clamp(scale, FlowChartModel.MinZoom, FlowChartModel.MaxZoom);
        scale = Math.Round(scale, 2);

        ViewModel.Model.ZoomLevel = scale;
        double scrollX = data.offsetX * scale;
        double scrollY = data.offsetY * scale;
        ScrollViewerHost.ScrollToHorizontalOffset(scrollX);
        ScrollViewerHost.ScrollToVerticalOffset(scrollY);
    }

    #endregion

    #region 辅助

    private Path CreatePendingConnectionPath()
    {
        var path = new Path();
        path.SetResourceReference(Path.StyleProperty, "ConnectionPathStyle_Pending");
        return path;
    }

    private void EnsureConnectionDragCache()
    {
        var source = ViewModel.ConnectionSourcePort;
        if (source == null || ReferenceEquals(source, _cachedConnectionSource)) return;

        _cachedConnectionTargets.Clear();
        _cachedConnectionSource = source;
        _cachedConnectionSourcePoint = new Point(0, 0);

        foreach (var connector in PortConnectorLocator.Enumerate(FlowChartCanvas))
        {
            if (connector.Port is not { } port) continue;
            Point center;
            try { center = PortConnectorLocator.GetCenter(connector, FlowChartCanvas); }
            catch (InvalidOperationException) { continue; }

            if (port.Id == source.Id)
                _cachedConnectionSourcePoint = center;
            else if (ViewModel.CanCompleteConnection(port))
                _cachedConnectionTargets.Add((connector, center));
        }
    }

    private void InvalidateConnectionDragCache()
    {
        _cachedConnectionSource = null;
        _cachedConnectionTargets.Clear();
    }

    private void UpdateConnectionCandidate(Point mousePosition)
    {
        PortConnectorView? nearest = null;
        var nearestPoint = default(Point);
        var nearestDistance = PortSnapRadius;

        foreach (var (connector, center) in _cachedConnectionTargets)
        {
            var distance = (center - mousePosition).Length;
            if (distance <= nearestDistance)
            {
                nearest = connector;
                nearestPoint = center;
                nearestDistance = distance;
            }
        }

        if (ReferenceEquals(nearest, _connectionCandidate))
            return;
        ClearConnectionCandidate();
        _connectionCandidate = nearest;
        _connectionCandidatePoint = nearestPoint;
        if (_connectionCandidate != null)
            _connectionCandidate.IsConnectionCandidate = true;
    }

    private void ClearConnectionCandidate()
    {
        if (_connectionCandidate != null)
            _connectionCandidate.IsConnectionCandidate = false;
        _connectionCandidate = null;
    }

    private void ResetConnectionDragCache()
    {
        ClearConnectionCandidate();
        InvalidateConnectionDragCache();
    }

    #endregion
}
