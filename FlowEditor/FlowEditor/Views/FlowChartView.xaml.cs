using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowEditor.Models;
using FlowEditor.Nodes;
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

    private readonly Dictionary<NodeViewModel, NodeView> _nodeViewCache = new();
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodeSubscriptions = new();
    private Point _boxSelectStart;
    private bool _isBoxSelecting;
    private PortConnectorView? _connectionCandidate;
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
        var nodeView = new NodeView { DataContext = vm };
        Canvas.SetLeft(nodeView, vm.X);
        Canvas.SetTop(nodeView, vm.Y);

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
            ViewModel.SelectedNode = vm;
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
        ClearConnectionCandidate();
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
            PendingCanvas.Children.Clear();
            if (ViewModel.PendingConnectionGeometry != null)
            {
                var path = new Path { Data = ViewModel.PendingConnectionGeometry };
                path.SetResourceReference(Path.StyleProperty, "ConnectionPathStyle_Pending");
                PendingCanvas.Children.Add(path);
            }
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

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus(); // 确保键盘焦点在 UserControl 上，否则 Delete 键无效

        ViewModel.SelectedNode = null;
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
        if (ViewModel.IsDrawingConnection)
        {
            var pos = e.GetPosition(FlowChartCanvas);
            ViewModel.UpdateConnectionEndPoint(pos);
            UpdateConnectionCandidate(pos);

            if (ViewModel.ConnectionSourcePort != null)
            {
                var sourceNode = ViewModel.FindNode(ViewModel.ConnectionSourcePort.OwnerNodeId);
                if (sourceNode != null && _nodeViewCache.TryGetValue(sourceNode, out var nodeView))
                {
                    var portCenter = FindPortCenterOnCanvas(nodeView, ViewModel.ConnectionSourcePort.Id);
                    var endPoint = _connectionCandidate == null
                        ? pos
                        : PortConnectorLocator.GetCenter(_connectionCandidate, FlowChartCanvas);
                    var geometry = ConnectionRenderer.BuildBezierGeometry(portCenter, endPoint);
                    ViewModel.SetPendingGeometry(geometry);
                }
            }
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isBoxSelecting) return;
        _isBoxSelecting = false;
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
        var menu = new ContextMenu();
        if (nodeVm.IsEditable)
        {
            var addInPort = new MenuItem
            {
                Header = "新增输入端口",
                Command = ViewModel.AddInputPortCommand,
                CommandParameter = nodeVm
            };
            menu.Items.Add(addInPort);

            var addOutPort = new MenuItem
            {
                Header = "新增输出端口",
                Command = ViewModel.AddOutputPortCommand,
                CommandParameter = nodeVm
            };
            menu.Items.Add(addOutPort);

            menu.Items.Add(new Separator());

            var copyItem = new MenuItem
            {
                Header = "复制节点",
                InputGestureText = "Ctrl+C",
                Command = ViewModel.CopyNodeCommand,
                CommandParameter = nodeVm
            };
            menu.Items.Add(copyItem);

            var deleteItem = new MenuItem
            {
                Header = "删除节点",
                InputGestureText = "Delete",
                Command = ViewModel.DeleteNodeCommand,
                CommandParameter = nodeVm
            };
            menu.Items.Add(deleteItem);
        }

        menu.Items.Add(new Separator());

        var permissionItem = new MenuItem { Header = "权限" };
        permissionItem.Items.Add(new MenuItem
        {
            Header = "正常",
            Command = ViewModel.SetNormalPermissionCommand,
            CommandParameter = nodeVm
        });
        permissionItem.Items.Add(new MenuItem
        {
            Header = "只读",
            Command = ViewModel.SetReadOnlyPermissionCommand,
            CommandParameter = nodeVm
        });
        permissionItem.Items.Add(new MenuItem
        {
            Header = "禁用",
            Command = ViewModel.SetDisabledPermissionCommand,
            CommandParameter = nodeVm
        });
        menu.Items.Add(permissionItem);

        menu.IsOpen = true;
    }

    private void ShowCanvasContextMenu()
    {
        var menu = new ContextMenu();

        var fitView = new MenuItem { Header = "自适应画布视图", Command = ViewModel.ZoomToFitCommand };
        menu.Items.Add(fitView);

        menu.Items.Add(new Separator());

        menu.Items.Add(new MenuItem { Header = "撤销", InputGestureText = "Ctrl+Z", Command = ViewModel.UndoCommand });
        menu.Items.Add(new MenuItem { Header = "重做", InputGestureText = "Ctrl+Y", Command = ViewModel.RedoCommand });

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
        if (e.Key == Key.Delete)
        {
            if (ViewModel.SelectedConnection != null)
            {
                ViewModel.DeleteConnectionCommand.Execute(ViewModel.SelectedConnection);
                e.Handled = true;
            }
            else if (ViewModel.SelectedNode != null)
            {
                ViewModel.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    #endregion

    #region 缩放控制

    private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
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

    private Point FindPortCenterOnCanvas(NodeView nodeView, string portId)
    {
        var connector = PortConnectorLocator.FindByPortId(nodeView, portId);
        if (connector == null) return new Point(0, 0);
        try
        {
            return PortConnectorLocator.GetCenter(connector, FlowChartCanvas);
        }
        catch { return new Point(0, 0); }
    }

    private void UpdateConnectionCandidate(Point mousePosition)
    {
        var source = ViewModel.ConnectionSourcePort;
        PortConnectorView? nearest = null;
        var nearestDistance = PortSnapRadius;

        foreach (var connector in PortConnectorLocator.Enumerate(FlowChartCanvas))
        {
            var target = connector.Port;
            if (source == null || target == null || !ViewModel.CanCompleteConnection(target))
                continue;

            var center = PortConnectorLocator.GetCenter(connector, FlowChartCanvas);
            var distance = (center - mousePosition).Length;
            if (distance <= nearestDistance)
            {
                nearest = connector;
                nearestDistance = distance;
            }
        }

        if (ReferenceEquals(nearest, _connectionCandidate))
            return;
        ClearConnectionCandidate();
        _connectionCandidate = nearest;
        if (_connectionCandidate != null)
            _connectionCandidate.IsConnectionCandidate = true;
    }

    private void ClearConnectionCandidate()
    {
        if (_connectionCandidate != null)
            _connectionCandidate.IsConnectionCandidate = false;
        _connectionCandidate = null;
    }

    #endregion
}
