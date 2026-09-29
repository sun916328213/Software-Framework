using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.Core;
using FlowEditor.ViewModels;

namespace FlowEditor.Views;

/// <summary>呈现并操作循环或并行等结构化执行容器。</summary>
public partial class ExecutionContainerView : UserControl
{
    /// <summary>初始化容器标题栏的拖拽交互。</summary>
    public ExecutionContainerView()
    {
        InitializeComponent();
        TitleBar.MouseLeftButtonDown += OnMouseDown;
        TitleBar.MouseLeftButtonUp += OnMouseUp;
    }

    /// <summary>选中容器并开始在所属画布中拖拽。</summary>
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

    /// <summary>根据当前指针位置更新容器拖拽。</summary>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not NodeViewModel node) return;
        var chart = FindParent<FlowChartView>(this);
        var canvas = FindParent<Canvas>(this);
        if (chart?.DataContext is FlowChartViewModel vm && canvas != null)
            vm.UpdateNodeDrag(node, e.GetPosition(canvas));
    }

    /// <summary>结束容器拖拽并释放标题栏鼠标捕获。</summary>
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NodeViewModel node &&
            FindParent<FlowChartView>(this)?.DataContext is FlowChartViewModel vm)
            vm.EndNodeDrag(node);
        TitleBar.ReleaseMouseCapture();
        TitleBar.MouseMove -= OnMouseMove;
    }

    /// <summary>把缩放手柄的位移应用到执行容器尺寸。</summary>
    private void OnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is not NodeViewModel { Model: IExecutionScopeContainer container }) return;
        container.ContainerWidth += e.HorizontalChange;
        container.ContainerHeight += e.VerticalChange;
    }

    /// <summary>缩放结束后重新计算节点与执行容器的归属关系。</summary>
    private void OnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is NodeViewModel node &&
            FindParent<FlowChartView>(this)?.DataContext is FlowChartViewModel vm)
            vm.RefreshContainerMembership(node);
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
