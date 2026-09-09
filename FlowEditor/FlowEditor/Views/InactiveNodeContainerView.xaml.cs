using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;

namespace FlowEditor.Views;

public partial class InactiveNodeContainerView : UserControl
{
    public InactiveNodeContainerView()
    {
        InitializeComponent();
        TitleBar.MouseLeftButtonDown += OnMouseDown;
        TitleBar.MouseLeftButtonUp += OnMouseUp;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NodeViewModel node) return;
        var chart = FindParent<FlowChartView>(this);
        var canvas = FindParent<Canvas>(this);
        if (chart?.DataContext is not FlowChartViewModel vm || canvas == null) return;
        vm.SetSelectedNodes([node]);
        vm.BeginNodeDrag(node, e.GetPosition(canvas));
        TitleBar.CaptureMouse();
        TitleBar.MouseMove += OnMouseMove;
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not NodeViewModel node) return;
        var chart = FindParent<FlowChartView>(this);
        var canvas = FindParent<Canvas>(this);
        if (chart?.DataContext is FlowChartViewModel vm && canvas != null)
            vm.UpdateNodeDrag(node, e.GetPosition(canvas));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NodeViewModel node &&
            FindParent<FlowChartView>(this)?.DataContext is FlowChartViewModel vm)
            vm.EndNodeDrag(node);
        TitleBar.ReleaseMouseCapture();
        TitleBar.MouseMove -= OnMouseMove;
    }

    private void OnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is not NodeViewModel { Model: InactiveNodeContainerNode container }) return;
        container.ContainerWidth += e.HorizontalChange;
        container.ContainerHeight += e.VerticalChange;
    }

    private void OnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is NodeViewModel node &&
            FindParent<FlowChartView>(this)?.DataContext is FlowChartViewModel vm)
            vm.RefreshContainerMembership(node);
    }

    private static T? FindParent<T>(DependencyObject element) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
