// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowChartContextMenuTests。覆盖画布右键菜单的弹出层捕获保持，以及连线手势取消时对他人鼠标捕获的尊重。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.Controls.Views;
using FlowEditor.Controls.Views.Interaction;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// 画布右键菜单回归测试。
/// 历史缺陷：右键空白画布时菜单弹出后立即消失（一闪即逝），
/// 根因是 OnCanvasRightClick 先开菜单、后调用连线手势的 Cancel()，而 Cancel 内部无差别执行
/// Mouse.Capture(null)，把 ContextMenu 弹出层刚建立的捕获释放掉，菜单随即自行关闭。
/// </summary>
[Collection("FlowEditorWpf")]
public class FlowChartContextMenuTests
{
    /// <summary>空白画布右键后，弹出层应继续持有鼠标捕获（即菜单保持打开）。</summary>
    [Fact]
    public void CanvasRightClick_KeepsContextMenuOpen()
    {
        WpfTestHost.Run(() =>
        {
            var application = Application.Current;
            var previousShutdownMode = application.ShutdownMode;
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new Window
            {
                Width = 700,
                Height = 500,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            try
            {
                var vm = TestFlowEditorFactory.CreateViewModel();
                var view = new FlowChartView { DataContext = vm };
                window.Content = view;
                window.Show();
                window.UpdateLayout();

                var zoomContainer = Assert.IsAssignableFrom<FrameworkElement>(view.FindName("ZoomContainer"));
                zoomContainer.RaiseEvent(new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Right)
                {
                    RoutedEvent = UIElement.MouseRightButtonDownEvent,
                    Source = zoomContainer
                });

                // ContextMenu 依靠弹出层的鼠标捕获判断“点击外部”；捕获被抢走即等价于菜单被关闭。
                Assert.NotNull(Mouse.Captured);
            }
            finally
            {
                Mouse.Capture(null);
                window.Close();
                application.ShutdownMode = previousShutdownMode;
            }
        });
    }

    /// <summary>未处于连线手势时，取消操作不得抢走其他控件（含右键菜单弹出层）的鼠标捕获。</summary>
    [Fact]
    public void Cancel_DoesNotReleaseCaptureOwnedByOtherControls()
    {
        WpfTestHost.Run(() =>
        {
            var application = Application.Current;
            var previousShutdownMode = application.ShutdownMode;
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var owner = new Border { Background = Brushes.Transparent };
            var nodeCanvas = new Canvas();
            var pendingCanvas = new Canvas();
            var panel = new Grid();
            panel.Children.Add(owner);
            panel.Children.Add(nodeCanvas);
            panel.Children.Add(pendingCanvas);
            var window = new Window
            {
                Content = panel,
                Width = 400,
                Height = 300,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            try
            {
                window.Show();
                window.UpdateLayout();

                var controller = new FlowConnectionDragController(nodeCanvas, pendingCanvas);
                Assert.True(owner.CaptureMouse());
                Assert.Same(owner, Mouse.Captured);

                controller.Cancel();

                Assert.Same(owner, Mouse.Captured);
            }
            finally
            {
                Mouse.Capture(null);
                window.Close();
                application.ShutdownMode = previousShutdownMode;
            }
        });
    }
}
