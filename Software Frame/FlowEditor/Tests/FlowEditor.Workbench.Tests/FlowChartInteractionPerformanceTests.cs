// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowChartInteractionPerformanceTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、System.Windows.Media、System.Windows.Shapes、System.Windows.Input、FlowEditor.Controls.Core。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Controls.Views;
using FlowEditor.Controls.Views.Interaction;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// FlowChartInteractionPerformanceTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class FlowChartInteractionPerformanceTests
{
    /// <summary>
    /// 验证 SelectingNode_SetsVisualStateBeforeSelectedNodeObserversRun 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、CreateViewModel、nameof、SetSelectedNodes；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SelectingNode_SetsVisualStateBeforeSelectedNodeObserversRun()
    {
        var service = TestFlowEditorFactory.CreateService();
        var node = service.Canvas.AddNode(new CountingNode { Name = "node" });
        var vm = TestFlowEditorFactory.CreateViewModel(service);
        bool? selectedWhenPropertyPanelObserverStarts = null;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FlowChartViewModel.SelectedNode))
                selectedWhenPropertyPanelObserverStarts = node.IsSelected;
        };

        vm.SetSelectedNodes([node]);

        Assert.True(selectedWhenPropertyPanelObserverStarts);
    }

    /// <summary>
    /// 验证 RapidNodeSelection_LastClickWinsWithoutTransientUnselectedNotification 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、ToArray、Select、Range、AddNode；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RapidNodeSelection_LastClickWinsWithoutTransientUnselectedNotification()
    {
        var service = TestFlowEditorFactory.CreateService();
        var nodes = Enumerable.Range(0, 64)
            .Select(index => service.Canvas.AddNode(new CountingNode { Name = $"node-{index}" }))
            .ToArray();
        var vm = TestFlowEditorFactory.CreateViewModel(service);
        var observerAlwaysSawSelectedNode = true;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FlowChartViewModel.SelectedNode))
                observerAlwaysSawSelectedNode &= vm.SelectedNode?.IsSelected == true;
        };

        for (var index = 0; index < 512; index++)
            vm.SetSelectedNodes([nodes[index % nodes.Length]]);

        var expected = nodes[511 % nodes.Length];
        Assert.Same(expected, vm.SelectedNode);
        Assert.Same(expected, Assert.Single(nodes, node => node.IsSelected));
        Assert.True(observerAlwaysSawSelectedNode);
    }

    /// <summary>
    /// 验证 CtrlLeftClick_IsReservedForMultiSelection_NotCanvasPan 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 False、ShouldStartCanvasPan、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CtrlLeftClick_IsReservedForMultiSelection_NotCanvasPan()
    {
        Assert.False(FlowViewportController.ShouldStartCanvasPan(MouseButton.Left, ModifierKeys.Control));
        Assert.True(FlowViewportController.ShouldStartCanvasPan(MouseButton.Left, ModifierKeys.Shift));
        Assert.True(FlowViewportController.ShouldStartCanvasPan(MouseButton.Middle, ModifierKeys.None));
    }

    /// <summary>
    /// 验证 ToggleNodeSelection_AccumulatesAndRemovesCtrlSelections 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、CreateViewModel、ToggleNodeSelection、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ToggleNodeSelection_AccumulatesAndRemovesCtrlSelections()
    {
        var service = TestFlowEditorFactory.CreateService();
        var first = service.Canvas.AddNode(new CountingNode { Name = "first" });
        var second = service.Canvas.AddNode(new CountingNode { Name = "second" });
        var vm = TestFlowEditorFactory.CreateViewModel(service);

        vm.ToggleNodeSelection(first);
        vm.ToggleNodeSelection(second);

        Assert.Equal(2, vm.SelectedNodes.Count);
        Assert.True(first.IsSelected);
        Assert.True(second.IsSelected);

        vm.ToggleNodeSelection(first);

        Assert.Equal([second], vm.SelectedNodes);
        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
    }

    /// <summary>
    /// 验证 NodeMouseDown_ShowsSelectedVisualInSameInputTurn 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Run、CreateService、AddNode、CreateViewModel、Measure；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NodeMouseDown_ShowsSelectedVisualInSameInputTurn()
    {
        WpfTestHost.Run(() =>
        {
            var service = TestFlowEditorFactory.CreateService();
            var node = service.Canvas.AddNode(new CountingNode { Name = "node" });
            var vm = TestFlowEditorFactory.CreateViewModel(service);
            var view = new FlowChartView { DataContext = vm, Width = 800, Height = 600 };
            view.Measure(new Size(800, 600));
            view.Arrange(new Rect(0, 0, 800, 600));
            view.UpdateLayout();

            var nodeView = FindVisualChildren<NodeView>(view).Single();
            var selectedIndicator = FindVisualChildren<Ellipse>(nodeView)
                .Single(ellipse => ellipse.Width == 10 && ellipse.Height == 10);
            Assert.NotEqual(Visibility.Visible, selectedIndicator.Visibility);

            nodeView.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = nodeView
            });

            Assert.True(node.IsSelected);
            Assert.Equal(Visibility.Visible, selectedIndicator.Visibility);
        });
    }

    /// <summary>
    /// 验证 PendingConnectionUpdates_ReuseSinglePathVisual 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Run、CreateService、CreateViewModel、IsType、FindName；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PendingConnectionUpdates_ReuseSinglePathVisual()
    {
        WpfTestHost.Run(() =>
        {
            var service = TestFlowEditorFactory.CreateService();
            var vm = TestFlowEditorFactory.CreateViewModel(service);
            var view = new FlowChartView { DataContext = vm };
            var pendingCanvas = Assert.IsType<Canvas>(view.FindName("PendingCanvas"));

            vm.SetPendingGeometry(new LineGeometry(new Point(0, 0), new Point(10, 10)));
            var firstPath = Assert.IsType<Path>(Assert.Single(pendingCanvas.Children));

            var nextGeometry = new LineGeometry(new Point(0, 0), new Point(20, 20));
            vm.SetPendingGeometry(nextGeometry);

            var updatedPath = Assert.IsType<Path>(Assert.Single(pendingCanvas.Children));
            Assert.Same(firstPath, updatedPath);
            Assert.Same(nextGeometry, updatedPath.Data);
        });
    }

    /// <summary>
    /// 验证 RepaintingConnections_ReusesExistingPathVisuals 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Run、New、Flow、RenderConnections、ToArray；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RepaintingConnections_ReusesExistingPathVisuals()
    {
        WpfTestHost.Run(() =>
        {
            var (source, _) = New<CountingNode>("source");
            var (target, _) = New<CountingNode>("target");
            var connection = Flow(source, target);
            var connectionCanvas = new Canvas();
            var nodeCanvas = new Canvas();

            ConnectionRenderer.RenderConnections(
                connectionCanvas, nodeCanvas, [connection], [source, target]);
            var firstVisuals = connectionCanvas.Children.Cast<Path>().ToArray();

            source.X = 50;
            ConnectionRenderer.RenderConnections(
                connectionCanvas, nodeCanvas, [connection], [source, target]);
            var updatedVisuals = connectionCanvas.Children.Cast<Path>().ToArray();

            Assert.Equal(2, updatedVisuals.Length);
            Assert.Same(firstVisuals[0], updatedVisuals[0]);
            Assert.Same(firstVisuals[1], updatedVisuals[1]);
        });
    }

    /// <summary>
    /// 验证 LoopContainerBody_DoesNotInterceptClicksIntendedForConnections 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Run、CreateService、AddNode、CreateViewModel、Measure；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoopContainerBody_DoesNotInterceptClicksIntendedForConnections()
    {
        WpfTestHost.Run(() =>
        {
            var service = TestFlowEditorFactory.CreateService();
            service.Canvas.AddNode(new LoopContainerNode { X = 100, Y = 100 });
            var vm = TestFlowEditorFactory.CreateViewModel(service);
            var view = new FlowChartView { DataContext = vm, Width = 1000, Height = 700 };
            view.Measure(new Size(1000, 700));
            view.Arrange(new Rect(0, 0, 1000, 700));
            view.UpdateLayout();

            var containerView = FindVisualChildren<ExecutionContainerView>(view).Single();
            var lanePresenter = Assert.IsType<ItemsControl>(containerView.FindName("LanePresenter"));

            Assert.False(lanePresenter.IsHitTestVisible);
        });
    }

    /// <summary>
    /// 验证 FindVisualChildren 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、FindVisualChildren；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

}
