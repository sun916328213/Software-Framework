// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowViewportController。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Views、System.Windows、System.Windows.Controls、System.Windows.Input、FlowEditor.Controls.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Controls.Views.Interaction;

/// <summary>
/// 集中管理流程画布的平移、鼠标定位缩放和内容自适应交互。
/// 事件订阅与手势状态均封装在此模块中，宿主视图只负责提供视觉元素和当前 ViewModel。
/// </summary>
internal sealed class FlowViewportController
{
    private readonly ScrollViewer _scrollViewer;
    private readonly FrameworkElement _zoomContainer;
    private readonly Func<FlowChartViewModel?> _getViewModel;
    private bool _isPanning;
    private MouseButton? _panButton;
    private Point _panStart;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;

    /// <summary>绑定视口元素并接管平移及滚轮事件。</summary>
    public FlowViewportController(
        ScrollViewer scrollViewer,
        FrameworkElement zoomContainer,
        Func<FlowChartViewModel?> getViewModel)
    {
        _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
        _zoomContainer = zoomContainer ?? throw new ArgumentNullException(nameof(zoomContainer));
        _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));

        _scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
        _zoomContainer.PreviewMouseDown += OnMouseDown;
        _zoomContainer.PreviewMouseMove += OnMouseMove;
        _zoomContainer.PreviewMouseUp += OnMouseUp;
    }

    /// <summary>当前是否正在通过鼠标拖动画布视口。</summary>
    public bool IsPanning => _isPanning;

    /// <summary>判断鼠标按键与修饰键是否表示画布平移手势。</summary>
    internal static bool ShouldStartCanvasPan(MouseButton button, ModifierKeys modifiers) =>
        button == MouseButton.Middle ||
        (button == MouseButton.Left && modifiers.HasFlag(ModifierKeys.Shift));

    /// <summary>响应流程请求，计算适配视口的缩放比例并滚动到内容起点。</summary>
    public void OnZoomToFitRequested(
        object? sender,
        (double contentWidth, double contentHeight, double offsetX, double offsetY) data)
    {
        var viewModel = _getViewModel();
        if (viewModel == null) return;

        var viewportWidth = _scrollViewer.ViewportWidth;
        var viewportHeight = _scrollViewer.ViewportHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        var scaleX = viewportWidth / data.contentWidth;
        var scaleY = viewportHeight / data.contentHeight;
        var scale = Math.Clamp(
            Math.Min(scaleX, scaleY),
            FlowChartModel.MinZoom,
            FlowChartModel.MaxZoom);

        viewModel.Model.ZoomLevel = Math.Round(scale, 2);
        _scrollViewer.ScrollToHorizontalOffset(data.offsetX * viewModel.Model.ZoomLevel);
        _scrollViewer.ScrollToVerticalOffset(data.offsetY * viewModel.Model.ZoomLevel);
    }

    /// <summary>记录平移起点、滚动偏移并捕获鼠标。</summary>
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_getViewModel() == null ||
            !ShouldStartCanvasPan(e.ChangedButton, Keyboard.Modifiers)) return;

        _isPanning = true;
        _panButton = e.ChangedButton;
        _panStart = e.GetPosition(_scrollViewer);
        _panStartHorizontalOffset = _scrollViewer.HorizontalOffset;
        _panStartVerticalOffset = _scrollViewer.VerticalOffset;
        _zoomContainer.Cursor = Cursors.Hand;
        _zoomContainer.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>根据鼠标位移更新水平和垂直滚动偏移。</summary>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        var panButtonPressed = _panButton switch
        {
            MouseButton.Middle => e.MiddleButton == MouseButtonState.Pressed,
            MouseButton.Left => e.LeftButton == MouseButtonState.Pressed,
            _ => false
        };
        if (!panButtonPressed) return;

        var current = e.GetPosition(_scrollViewer);
        var delta = current - _panStart;
        _scrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - delta.X);
        _scrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - delta.Y);
        e.Handled = true;
    }

    /// <summary>结束画布平移并恢复鼠标状态。</summary>
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning || e.ChangedButton != _panButton) return;
        _isPanning = false;
        _panButton = null;
        _zoomContainer.ReleaseMouseCapture();
        _zoomContainer.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    /// <summary>以鼠标位置为中心调整画布倍率和滚动偏移。</summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var viewModel = _getViewModel();
        if (viewModel == null) return;
        e.Handled = true;

        var containerPosition = e.GetPosition(_zoomContainer);
        var mousePosition = e.GetPosition(_scrollViewer);
        var oldZoom = viewModel.Model.ZoomLevel;

        if (e.Delta > 0)
            viewModel.ZoomInCommand.Execute(null);
        else
            viewModel.ZoomOutCommand.Execute(null);

        var newZoom = viewModel.Model.ZoomLevel;
        if (Math.Abs(newZoom - oldZoom) < 0.001) return;

        _scrollViewer.ScrollToHorizontalOffset(containerPosition.X * newZoom - mousePosition.X);
        _scrollViewer.ScrollToVerticalOffset(containerPosition.Y * newZoom - mousePosition.Y);
    }
}
