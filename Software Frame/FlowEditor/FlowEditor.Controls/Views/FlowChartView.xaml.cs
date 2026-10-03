// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Collections.Specialized、System.ComponentModel、System.Windows、System.Windows.Controls、System.Windows.Input。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Controls.Views.Interaction;

namespace FlowEditor.Controls.Views;

/// <summary>
/// 流程图画布视图（纯画布，不含工具箱/属性/输出/工具栏）。
/// 由 FlowChartContainerView 以标签页方式托管。
/// </summary>
public partial class FlowChartView : UserControl
{
    private FlowChartViewModel? _currentViewModel;
    private bool _isViewSuspended;
    private FlowChartViewModel ViewModel => _currentViewModel!;

    private readonly Dictionary<NodeViewModel, FrameworkElement> _nodeViewCache = new();
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodeSubscriptions = new();
    private readonly FlowViewportController _viewport;
    private readonly FlowConnectionDragController _connectionDrag;
    private readonly FlowSelectionController _selection;

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
        _connectionDrag = new FlowConnectionDragController(FlowChartCanvas, PendingCanvas);
        _selection = new FlowSelectionController(
            FlowChartCanvas,
            ConnectionCanvas,
            SelectionRectangle,
            () => _currentViewModel,
            () => _viewport.IsPanning,
            _connectionDrag.Cancel,
            () => Focus());
        DataContextChanged += OnDataContextChanged;
        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;
        PreviewKeyDown += OnFlowChartKeyDown;
        PreviewMouseDown += (s, e) => Focus(); // 确保键盘焦点在画布上，Delete 键才有效
    }

    #region ViewModel 绑定与切换

    /// <summary>切换流程上下文时释放旧订阅、绑定新会话并重建节点视觉树。</summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        => AttachViewModel(_isViewSuspended ? null : DataContext as FlowChartViewModel);

    /// <summary>重新显示页面时恢复当前上下文的视觉订阅。</summary>
    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        _isViewSuspended = false;
        AttachViewModel(DataContext as FlowChartViewModel);
    }

    /// <summary>卸载页面时解除视觉订阅，不改变流程会话的执行状态。</summary>
    private void OnViewUnloaded(object sender, RoutedEventArgs e)
    {
        _isViewSuspended = true;
        AttachViewModel(null);
    }

    /// <summary>协调画布绑定并释放旧上下文、节点和连线的视觉状态。</summary>
    private void AttachViewModel(FlowChartViewModel? viewModel)
    {
        if (ReferenceEquals(_currentViewModel, viewModel)) return;
        _connectionDrag.Attach(null);
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
        ConnectionCanvas.Children.Clear();

        // 订阅新 ViewModel
        _currentViewModel = viewModel;
        if (_currentViewModel == null) return;

        _currentViewModel.Nodes.CollectionChanged += OnNodesCollectionChanged;
        _currentViewModel.Connections.CollectionChanged += OnConnectionsCollectionChanged;
        _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _currentViewModel.Model.PropertyChanged += OnModelPropertyChanged;
        _currentViewModel.ZoomToFitRequested += _viewport.OnZoomToFitRequested;
        _connectionDrag.Attach(_currentViewModel);

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

        FlowChartCanvas.Children.Add(nodeView);
        _nodeViewCache[vm] = nodeView;
        _nodeSubscriptions[vm] = propertyChanged;
        _selection.RegisterNode(vm, nodeView);
    }

    /// <summary>移除节点视觉对象并解除其属性变化订阅。</summary>
    private void RemoveNodeViewFromCanvas(NodeViewModel vm)
    {
        _selection.UnregisterNode(vm);
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
        _selection.ClearNodes();
        FlowChartCanvas.Children.Clear();
        _connectionDrag.InvalidateLayout();
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
            // 重绘排队期间，标签内容可能已被移除并清空上下文。
            if (_currentViewModel == null) return;

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

    /// <summary>同步选中连线等需要重绘的 ViewModel 状态。</summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowChartViewModel.SelectedConnection))
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
        _connectionDrag.Cancel();
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

}
