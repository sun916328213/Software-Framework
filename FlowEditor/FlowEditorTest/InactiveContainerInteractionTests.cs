using System.IO;
using System.Windows;
using FlowEditor.Core;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
using FlowEditor.ViewModels;
using Xunit;

namespace FlowEditorTest;

public class InactiveContainerInteractionTests
{
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

    [Fact]
    public void Container_ExposesResizableDimensions()
    {
        var width = typeof(InactiveNodeContainerNode).GetProperty("ContainerWidth");
        var height = typeof(InactiveNodeContainerNode).GetProperty("ContainerHeight");

        Assert.True(width?.CanWrite == true);
        Assert.True(height?.CanWrite == true);
    }

    [Fact]
    public void ContainerView_UsesSameSelectedStateAsNodes_AndCanvasSizeIsBound()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var containerXaml = File.ReadAllText(Path.Combine(root,
            "FlowEditor", "Views", "InactiveNodeContainerView.xaml"));
        var chartXaml = File.ReadAllText(Path.Combine(root,
            "FlowEditor", "Views", "FlowChartView.xaml"));

        Assert.Contains("x:Name=\"SelectionIndicator\"", containerXaml);
        Assert.Contains("Visibility=\"{Binding IsSelected", containerXaml);
        Assert.Contains("BorderBrush=\"{Binding Status", containerXaml);
        Assert.Contains("Width=\"{Binding Model.CanvasWidth}\"", chartXaml);
        Assert.Contains("Height=\"{Binding Model.CanvasHeight}\"", chartXaml);
    }
}
