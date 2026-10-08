using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Controls.Views;

/// <summary>复用两种容器视图的标题栏拖拽交互。</summary>
internal sealed class ContainerTitleDrag(UserControl view, UIElement titleBar)
{
    /// <summary>订阅标题栏的按下与释放事件。</summary>
    internal void Attach()
    {
        titleBar.MouseLeftButtonDown += OnMouseDown;
        titleBar.MouseLeftButtonUp += OnMouseUp;
    }

    /// <summary>缩放完成后更新所属画布中的容器归属。</summary>
    internal static void RefreshMembership(UserControl view)
    {
        if (view.DataContext is NodeViewModel node &&
            FindParent<FlowChartView>(view)?.DataContext is FlowChartViewModel vm)
            vm.RefreshContainerMembership(node);
    }

    /// <summary>选中容器并开始在所属画布中拖拽。</summary>
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (view.DataContext is not NodeViewModel node) return;
        var chart = FindParent<FlowChartView>(view);
        var canvas = FindParent<Canvas>(view);
        if (chart?.DataContext is not FlowChartViewModel vm || canvas == null) return;
        vm.SetSelectedNodes([node]);
        vm.BeginNodeDrag(node, e.GetPosition(canvas));
        titleBar.CaptureMouse();
        titleBar.MouseMove += OnMouseMove;
        e.Handled = true;
    }

    /// <summary>根据当前指针位置更新容器拖拽。</summary>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (view.DataContext is not NodeViewModel node) return;
        var chart = FindParent<FlowChartView>(view);
        var canvas = FindParent<Canvas>(view);
        if (chart?.DataContext is FlowChartViewModel vm && canvas != null)
            vm.UpdateNodeDrag(node, e.GetPosition(canvas));
    }

    /// <summary>结束容器拖拽并释放标题栏鼠标捕获。</summary>
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (view.DataContext is NodeViewModel node &&
            FindParent<FlowChartView>(view)?.DataContext is FlowChartViewModel vm)
            vm.EndNodeDrag(node);
        titleBar.ReleaseMouseCapture();
        titleBar.MouseMove -= OnMouseMove;
    }

    /// <summary>沿视觉树查找指定类型的父元素。</summary>
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
