using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.Models;
using FlowEditor.ViewModels;

namespace FlowEditor.Views;

/// <summary>
/// 节点视图代码隐藏。
/// 处理标题栏拖拽和端口连线鼠标事件。
/// </summary>
public partial class NodeView : UserControl
{
    private Canvas? _dragCanvas;
    private FlowChartViewModel? _dragFlowChartViewModel;

    /// <summary>初始化节点标题拖拽和端口连线交互。</summary>
    public NodeView()
    {
        InitializeComponent();
        TitleBar.MouseLeftButtonDown += OnTitleBarMouseDown;
        TitleBar.MouseLeftButtonUp += OnTitleBarMouseUp;
        InputPortsItemsControl.AddHandler(MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPortMouseDown), true);
        OutputPortsItemsControl.AddHandler(MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPortMouseDown), true);
    }

    #region 标题栏拖拽

    /// <summary>选择节点并开始单节点或多选节点拖拽。</summary>
    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NodeViewModel vm) return;
        if (!vm.IsDraggable) return;

        var flowChartVm = FindFlowChartViewModel(this);
        if (flowChartVm != null)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                flowChartVm.ToggleNodeSelection(vm);
                if (!vm.IsSelected)
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (!vm.IsSelected)
            {
                flowChartVm.SetSelectedNodes([vm]);
            }
        }

        var canvas = FindParentCanvas(this);
        if (canvas == null) return;

        _dragCanvas = canvas;
        _dragFlowChartViewModel = flowChartVm;

        var mousePos = e.GetPosition(canvas);
        if (flowChartVm != null) flowChartVm.BeginNodeDrag(vm, mousePos);
        else vm.BeginDrag(mousePos);

        TitleBar.CaptureMouse();
        TitleBar.MouseMove += OnTitleBarMouseMove;
        e.Handled = true;
    }

    /// <summary>根据标题栏指针位置更新当前节点拖拽。</summary>
    private void OnTitleBarMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not NodeViewModel vm || !vm.IsDragging) return;
        var canvas = _dragCanvas;
        if (canvas == null) return;
        var mousePos = e.GetPosition(canvas);
        var flowChartVm = _dragFlowChartViewModel;
        if (flowChartVm != null) flowChartVm.UpdateNodeDrag(vm, mousePos);
        else vm.UpdateDrag(mousePos);
    }

    /// <summary>结束节点拖拽并释放标题栏鼠标捕获。</summary>
    private void OnTitleBarMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NodeViewModel vm) return;
        TitleBar.ReleaseMouseCapture();
        TitleBar.MouseMove -= OnTitleBarMouseMove;
        var flowChartVm = _dragFlowChartViewModel;
        if (flowChartVm != null) flowChartVm.EndNodeDrag(vm);
        else vm.EndDrag();
        _dragCanvas = null;
        _dragFlowChartViewModel = null;
    }

    #endregion

    #region 端口连线事件

    /// <summary>从被按下的输出端口开始创建临时连线。</summary>
    private void OnPortMouseDown(object sender, MouseButtonEventArgs e)
    {
        var connector = PortConnectorLocator.FindAncestor(e.OriginalSource as DependencyObject);
        if (connector?.Port is not PortModel port) return;
        if (port.Direction != PortDirection.Output) return;

        var flowChartVm = FindFlowChartViewModel(this);
        if (flowChartVm == null) return;

        flowChartVm.StartConnection(port);

        connector.CaptureMouse();
        e.Handled = true;
    }

    #endregion

    #region 辅助方法

    /// <summary>查找节点所属的流程画布，并避开连线覆盖画布。</summary>
    private static Canvas? FindParentCanvas(DependencyObject element)
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current != null)
        {
            if (current is Canvas canvas && canvas.Name == "FlowChartCanvas")
                return canvas;
            current = VisualTreeHelper.GetParent(current);
        }
        current = VisualTreeHelper.GetParent(element);
        while (current != null)
        {
            if (current is Canvas canvas && canvas.Name != "ConnectionCanvas")
                return canvas;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>沿视觉树查找节点所属的流程图 ViewModel。</summary>
    private static FlowChartViewModel? FindFlowChartViewModel(DependencyObject element)
    {
        var current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is FlowChartViewModel vm)
                return vm;
            current = VisualTreeHelper.GetParent(current) as DependencyObject;
        }
        return null;
    }

    #endregion
}
