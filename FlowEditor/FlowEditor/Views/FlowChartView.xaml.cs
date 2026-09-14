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
using FlowEditor.Views.Interaction;

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
    private readonly FlowViewportController _viewport;
    private Point _boxSelectStart;
    private bool _isBoxSelecting;
    private PortConnectorView? _connectionCandidate;
    private Point _connectionCandidatePoint;
    private Path? _pendingConnectionPath;
    private PortModel? _cachedConnectionSource;
    private Point _cachedConnectionSourcePoint;
    private readonly List<(PortConnectorView Connector, Point Center)> _cachedConnectionTargets = [];
    private const double PortSnapRadius = 28;

    /// <summary>暴露画布容器供容器层导出 PNG 等操作使用。</summary>
    public Grid ZoomContainerElement => ZoomContainer;

    /// <summary>初始化画布视图并注册数据上下文、键盘和连线完成事件。</summary>
    public FlowChartView()
    {
        InitializeComponent();
        _viewport = new FlowViewportController(
            ScrollViewerHost,
            ZoomContainer,
            () => _currentViewModel);
        DataContextChanged += OnDataContextChanged;
        PreviewKeyDown += OnFlowChartKeyDown;
        PreviewMouseDown += (s, e) => Focus(); // 确保键盘焦点在画布上，Delete 键才有效
        FlowChartCanvas.AddHandler(
            Mouse.MouseUpEvent,
            new MouseButtonEventHandler(OnCanvasConnectionMouseUp),
            handledEventsToo: true);
    }

    #region ViewModel 绑定与切换

    /// <summary>切换流程上下文时释放旧订阅、绑定新会话并重建节点视觉树。</summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 取消旧 ViewModel 及 Model 订阅
        if (_currentViewModel != null)
        {
            _currentViewModel.Nodes.CollectionChanged -= OnNodesCollectionChanged;
            _currentViewModel.Connections.CollectionChanged -= OnConnectionsCollectionChanged;
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _currentViewModel.Model.PropertyChanged -= OnModelPropertyChanged;
            _currentViewModel.ZoomToFitRequested -= _viewport.OnZoomToFitRequested;
        }
        ClearNodeViews();

        // 订阅新 ViewModel
        _currentViewModel = DataContext as FlowChartViewModel;
        if (_currentViewModel == null) return;

        _currentViewModel.Nodes.CollectionChanged += OnNodesCollectionChanged;
        _currentViewModel.Connections.CollectionChanged += OnConnectionsCollectionChanged;
        _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _currentViewModel.Model.PropertyChanged += OnModelPropertyChanged;
        _currentViewModel.ZoomToFitRequested += _viewport.OnZoomToFitRequested;

        RenderAllNodes();
    }

    /// <summary>连线集合变化后合并调度一次重绘。</summary>
    private void OnConnectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScheduleConnectionRender();

    #endregion

    #region 节点渲染

    /// <summary>把节点集合的增删和重置同步到画布视觉树。</summary>
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

    /// <summary>从当前流程重新构建全部节点视觉对象并调度连线重绘。</summary>
    private void RenderAllNodes()
    {
        ClearNodeViews();
        foreach (var vm in ViewModel.Nodes)
            AddNodeViewToCanvas(vm);
        ScheduleConnectionRender();
    }

    /// <summary>按节点类型创建视图、设置层级并建立坐标变化订阅。</summary>
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

    /// <summary>移除节点视觉对象并解除其属性变化订阅。</summary>
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

    /// <summary>释放全部节点订阅并清空节点视觉与临时连线缓存。</summary>
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

    /// <summary>在布局完成优先级合并连线重绘请求，避免连续变化重复渲染。</summary>
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

    /// <summary>同步临时连线、拖拽缓存和选中连线等 ViewModel 状态。</summary>
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

    /// <summary>模型显示选项变化时刷新连线图层。</summary>
    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowChartModel.ShowDataConnections))
        {
            ScheduleConnectionRender();
        }
    }

    #endregion

    #region 画布鼠标事件

    /// <summary>仅允许工具箱节点以复制方式拖入画布。</summary>
    private void OnCanvasDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(NodePaletteItem))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>在鼠标所在画布坐标创建拖入的工具箱节点。</summary>
    private void OnCanvasDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(NodePaletteItem)) is not NodePaletteItem item) return;
        ViewModel.AddNodeAt(item, e.GetPosition(ZoomContainer));
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    /// <summary>处理连线选中、空白处取消及框选状态的开始。</summary>
    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewport.IsPanning || e.ChangedButton != MouseButton.Left) return;
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

    /// <summary>在候选端口提交临时连线，否则取消本次连线。</summary>
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

    /// <summary>更新框选区域或临时连线的吸附候选和几何图形。</summary>
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

    /// <summary>提交框选结果并释放画布鼠标捕获。</summary>
    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isBoxSelecting) return;
        UpdateBoxSelection(e.GetPosition(FlowChartCanvas));
        _isBoxSelecting = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        FlowChartCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>更新选择矩形并选中与其相交的节点。</summary>
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

    /// <summary>根据命中节点打开节点菜单，否则打开画布菜单。</summary>
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

    /// <summary>使用当前节点和画布命令上下文打开节点菜单。</summary>
    private void ShowNodeContextMenu(NodeViewModel nodeVm)
    {
        var menu = (ContextMenu)FindResource("NodeContextMenu");
        menu.DataContext = new NodeContextMenuContext(ViewModel, nodeVm);
        menu.IsOpen = true;
    }

    /// <summary>使用当前流程上下文打开空白画布菜单。</summary>
    private void ShowCanvasContextMenu()
    {
        var menu = (ContextMenu)FindResource("CanvasContextMenu");
        menu.DataContext = ViewModel;
        menu.IsOpen = true;
    }

    /// <summary>沿视觉树向上查找命中元素所属的节点视图。</summary>
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

    /// <summary>把复制、粘贴和删除快捷键转发到当前流程命令。</summary>
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

    #region 辅助

    /// <summary>创建使用统一资源样式的临时连线路径。</summary>
    private Path CreatePendingConnectionPath()
    {
        var path = new Path();
        path.SetResourceReference(Path.StyleProperty, "ConnectionPathStyle_Pending");
        return path;
    }

    /// <summary>为当前源端口缓存坐标及可连接的目标端口。</summary>
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

    /// <summary>使源端口和候选目标缓存失效。</summary>
    private void InvalidateConnectionDragCache()
    {
        _cachedConnectionSource = null;
        _cachedConnectionTargets.Clear();
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

    /// <summary>清除当前连接候选及其高亮状态。</summary>
    private void ClearConnectionCandidate()
    {
        if (_connectionCandidate != null)
            _connectionCandidate.IsConnectionCandidate = false;
        _connectionCandidate = null;
    }

    /// <summary>清除候选端口并重置全部连线拖动缓存。</summary>
    private void ResetConnectionDragCache()
    {
        ClearConnectionCandidate();
        InvalidateConnectionDragCache();
    }

    #endregion
}
