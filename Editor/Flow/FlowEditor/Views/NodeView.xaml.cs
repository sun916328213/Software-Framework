using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowEditor.Models;
using FlowEditor.ViewModels;

namespace FlowEditor.Views;

/// <summary>
/// 节点视图代码隐藏。
/// 处理标题栏拖拽和端口连线鼠标事件。
/// </summary>
public partial class NodeView : UserControl
{
    public NodeView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleBar.MouseLeftButtonDown += OnTitleBarMouseDown;
        TitleBar.MouseLeftButtonUp += OnTitleBarMouseUp;

        var inputIc = (ItemsControl)FindName("InputPortsItemsControl");
        var outputIc = (ItemsControl)FindName("OutputPortsItemsControl");

        if (inputIc != null)
            inputIc.AddHandler(Ellipse.MouseLeftButtonDownEvent,
                (MouseButtonEventHandler)OnPortEllipseMouseDown, true);
        if (outputIc != null)
            outputIc.AddHandler(Ellipse.MouseLeftButtonDownEvent,
                (MouseButtonEventHandler)OnPortEllipseMouseDown, true);
    }

    #region 标题栏拖拽

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NodeViewModel vm) return;
        if (!vm.IsDraggable) return;

        var flowChartVm = FindFlowChartViewModel(this);
        if (flowChartVm != null)
            flowChartVm.SelectedNode = vm;

        var canvas = FindParentCanvas(this);
        if (canvas == null) return;

        var mousePos = e.GetPosition(canvas);
        vm.BeginDrag(mousePos);

        TitleBar.CaptureMouse();
        TitleBar.MouseMove += OnTitleBarMouseMove;
        e.Handled = true;
    }

    private void OnTitleBarMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not NodeViewModel vm || !vm.IsDragging) return;
        var canvas = FindParentCanvas(this);
        if (canvas == null) return;
        var mousePos = e.GetPosition(canvas);
        vm.UpdateDrag(mousePos);
    }

    private void OnTitleBarMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NodeViewModel vm) return;
        TitleBar.ReleaseMouseCapture();
        TitleBar.MouseMove -= OnTitleBarMouseMove;
        vm.EndDrag();
    }

    #endregion

    #region 端口连线事件

    private void OnPortEllipseMouseDown(object sender, MouseButtonEventArgs e)
    {
        var ellipse = FindAncestorEllipseWithName(e.OriginalSource as DependencyObject);
        if (ellipse == null || ellipse.Name != "PortIndicator") return;
        if (ellipse.Tag is not PortModel port) return;
        if (port.Direction != PortDirection.Output) return;

        var flowChartVm = FindFlowChartViewModel(this);
        if (flowChartVm == null) return;

        var canvas = FindParentCanvas(this);
        if (canvas == null) return;

        var portCenter = GetPortCenter(ellipse, canvas);
        flowChartVm.StartConnection(port, portCenter);

        ellipse.CaptureMouse();
        ellipse.MouseMove += (_, me) => OnPortMouseMove(me);
        ellipse.MouseLeftButtonUp += (_, _) => ellipse.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnPortMouseMove(MouseEventArgs e)
    {
        var flowChartVm = FindFlowChartViewModel(this);
        if (flowChartVm == null || !flowChartVm.IsDrawingConnection) return;

        var canvas = FindParentCanvas(this);
        if (canvas == null) return;

        var mousePos = e.GetPosition(canvas);
        flowChartVm.UpdateConnectionEndPoint(mousePos);

        var sourcePortCenter = flowChartVm.ConnectionSourcePort != null
            ? FindPortCenterOnCanvas(flowChartVm.ConnectionSourcePort.Id, canvas)
            : mousePos;

        var geometry = ConnectionRenderer.BuildBezierGeometry(sourcePortCenter, mousePos);
        flowChartVm.SetPendingGeometry(geometry);
    }

    private static Ellipse? FindAncestorEllipseWithName(DependencyObject? element)
    {
        var current = element;
        while (current != null)
        {
            if (current is Ellipse ellipse && !string.IsNullOrEmpty(ellipse.Name))
                return ellipse;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    #endregion

    #region 辅助方法

    private static Point GetPortCenter(Ellipse portEllipse, Visual relativeTo)
    {
        var center = new Point(portEllipse.ActualWidth / 2.0, portEllipse.ActualHeight / 2.0);
        var transform = portEllipse.TransformToVisual(relativeTo);
        return transform.Transform(center);
    }

    private Point FindPortCenterOnCanvas(string portId, Canvas canvas)
    {
        var ellipse = FindPortEllipse(this, portId);
        if (ellipse == null) return new Point(0, 0);
        return GetPortCenter(ellipse, canvas);
    }

    private static Ellipse? FindPortEllipse(DependencyObject root, string portId)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Ellipse ellipse && ellipse.Name == "PortIndicator" &&
                ellipse.Tag is PortModel port && port.Id == portId)
                return ellipse;
            var found = FindPortEllipse(child, portId);
            if (found != null) return found;
        }
        return null;
    }

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