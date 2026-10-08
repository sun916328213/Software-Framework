// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：InactiveContainerInteractionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Windows、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Nodes.FlowControl。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FlowEditor.Controls.Views;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.ViewModels;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// InactiveContainerInteractionTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class InactiveContainerInteractionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContainerTitle_RoutedMouseEventsSelectCaptureAndRelease(bool executionContainer)
    {
        WpfTestHost.Run(() =>
        {
            var application = Application.Current;
            var shutdownMode = application.ShutdownMode;
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var service = TestFlowEditorFactory.CreateService();
            var vm = TestFlowEditorFactory.CreateViewModel(service);
            var node = executionContainer
                ? service.Canvas.AddNode(new LoopContainerNode { X = 100, Y = 100 })
                : service.Canvas.AddNode(new InactiveNodeContainerNode { X = 100, Y = 100 });
            UserControl view = executionContainer ? new ExecutionContainerView() : new InactiveNodeContainerView();
            view.DataContext = node;
            var canvas = new Canvas();
            canvas.Children.Add(view);
            var chart = new FlowChartView { DataContext = vm, Content = canvas };
            var window = new Window { Content = chart, Width = 700, Height = 500, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var title = Assert.IsAssignableFrom<UIElement>(view.FindName("TitleBar"));
                var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
                title.RaiseEvent(down);
                Assert.True(down.Handled);
                Assert.True(node.IsSelected);
                Assert.Same(title, Mouse.Captured);
                title.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                    { RoutedEvent = UIElement.MouseMoveEvent });
                title.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                Assert.NotSame(title, Mouse.Captured);
                var releasedPosition = new Point(node.X, node.Y);
                title.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                    { RoutedEvent = UIElement.MouseMoveEvent });
                Assert.Equal(releasedPosition, new Point(node.X, node.Y));

                var thumb = Assert.Single(Assert.IsType<Grid>(view.Content).Children.OfType<Thumb>());
                var before = view.Width;
                thumb.RaiseEvent(new DragDeltaEventArgs(30, 20) { RoutedEvent = Thumb.DragDeltaEvent });
                window.UpdateLayout();
                Assert.Equal(before + 30, view.Width);
                thumb.RaiseEvent(new DragCompletedEventArgs(30, 20, false) { RoutedEvent = Thumb.DragCompletedEvent });
            }
            finally
            {
                window.Close();
                application.ShutdownMode = shutdownMode;
            }
        });
    }

    /// <summary>
    /// 测试相关入口 DragContainer_MovesContainedNodesBySameDelta；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateService、CreateViewModel、AddNode、SetSelectedNodes、BeginNodeDrag；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DragContainer_MovesContainedNodesBySameDelta()
    {
        var service = TestFlowEditorFactory.CreateService();
        var vm = TestFlowEditorFactory.CreateViewModel(service);
        var container = service.Canvas.AddNode(new InactiveNodeContainerNode { X = 100, Y = 100 });
        var child = service.Canvas.AddNode(new CountingNode
        {
            Name = "Child", X = 150, Y = 200, IsExcludedFromExecution = true
        });

        vm.SetSelectedNodes([container]);
        vm.BeginNodeDrag(container, new Point(100, 100));
        vm.UpdateNodeDrag(container, new Point(130, 140));
        vm.EndNodeDrag(container);

        Assert.Equal(130, container.X);
        Assert.Equal(140, container.Y);
        Assert.Equal(180, child.X);
        Assert.Equal(240, child.Y);
    }

    /// <summary>
    /// 测试相关入口 Container_ExposesResizableDimensions；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetProperty、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Container_ExposesResizableDimensions()
    {
        var width = typeof(InactiveNodeContainerNode).GetProperty("ContainerWidth");
        var height = typeof(InactiveNodeContainerNode).GetProperty("ContainerHeight");

        Assert.True(width?.CanWrite == true);
        Assert.True(height?.CanWrite == true);
    }

    /// <summary>
    /// 测试相关入口 ContainerView_UsesSameSelectedStateAsNodes_AndCanvasSizeIsBound；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ReadAllText、Combine、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ContainerView_UsesSameSelectedStateAsNodes_AndCanvasSizeIsBound()
    {
        var root = TestModulePaths.ModuleDirectory;
        var containerXaml = File.ReadAllText(Path.Combine(root,
            "FlowEditor.Controls", "Views", "InactiveNodeContainerView.xaml"));
        var chartXaml = File.ReadAllText(Path.Combine(root,
            "FlowEditor.Controls", "Views", "FlowChartView.xaml"));

        Assert.Contains("x:Name=\"SelectionIndicator\"", containerXaml);
        Assert.Contains("Visibility=\"{Binding IsSelected", containerXaml);
        Assert.Contains("BorderBrush=\"{Binding Status", containerXaml);
        Assert.Contains("Width=\"{Binding Model.CanvasWidth}\"", chartXaml);
        Assert.Contains("Height=\"{Binding Model.CanvasHeight}\"", chartXaml);
    }
}
