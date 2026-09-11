using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using FlowEditor.Core;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
using FlowEditor.ViewModels;
using FlowEditor.Views;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

public class FlowChartInteractionPerformanceTests
{
    [Fact]
    public void SelectingNode_SetsVisualStateBeforeSelectedNodeObserversRun()
    {
        var service = TestFlowEditorFactory.CreateService();
        var node = service.AddNode(new CountingNode { Name = "node" });
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

    [Fact]
    public void RapidNodeSelection_LastClickWinsWithoutTransientUnselectedNotification()
    {
        var service = TestFlowEditorFactory.CreateService();
        var nodes = Enumerable.Range(0, 64)
            .Select(index => service.AddNode(new CountingNode { Name = $"node-{index}" }))
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

    [Fact]
    public void CtrlLeftClick_IsReservedForMultiSelection_NotCanvasPan()
    {
        Assert.False(FlowChartView.ShouldStartCanvasPan(MouseButton.Left, ModifierKeys.Control));
        Assert.True(FlowChartView.ShouldStartCanvasPan(MouseButton.Left, ModifierKeys.Shift));
        Assert.True(FlowChartView.ShouldStartCanvasPan(MouseButton.Middle, ModifierKeys.None));
    }

    [Fact]
    public void ToggleNodeSelection_AccumulatesAndRemovesCtrlSelections()
    {
        var service = TestFlowEditorFactory.CreateService();
        var first = service.AddNode(new CountingNode { Name = "first" });
        var second = service.AddNode(new CountingNode { Name = "second" });
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

    [Fact]
    public void NodeMouseDown_ShowsSelectedVisualInSameInputTurn()
    {
        WpfTestHost.Run(() =>
        {
            var service = TestFlowEditorFactory.CreateService();
            var node = service.AddNode(new CountingNode { Name = "node" });
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

    [Fact]
    public void LoopContainerBody_DoesNotInterceptClicksIntendedForConnections()
    {
        WpfTestHost.Run(() =>
        {
            var service = TestFlowEditorFactory.CreateService();
            service.AddNode(new LoopContainerNode { X = 100, Y = 100 });
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
