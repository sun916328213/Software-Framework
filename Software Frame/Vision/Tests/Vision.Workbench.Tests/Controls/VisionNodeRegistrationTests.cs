using System.IO;
using System.Windows;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using Vision.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;
using Prism.Ioc;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionNodeRegistrationTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task CombinedCatalog_IsMetadataOnly_AndKeepsHiddenTypesOutOfPalette()
    {
        var created = 0;
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create(() => created++));
        try
        {
            host.Invoke(() =>
            {
                var items = Items(vm).ToArray();
                Assert.Equal(35, items.Length);
                Assert.Equal(0, created);
                Assert.Empty(vm.Chart.Nodes);
                Assert.Single(items, item => item.DisplayName == "条件分发");
                Assert.DoesNotContain(items, item => item.DisplayName is "条件分支" or "ToolBlock" or "循环容器" or "不执行容器" or "并行分叉");
                vm.ToolboxSearchText = "流程控制";
                Assert.Equal(7, Items(vm).Count());
                vm.ToolboxSearchText = "等待指定毫秒";
                Assert.Equal("延时", Assert.Single(Items(vm)).DisplayName);
                Assert.Equal(0, created);
                var discovery = ContainerLocator.Container.Resolve<IFlowToolDiscoveryService>();
                discovery.Discover();
                Assert.Equal(35, discovery.DiscoveredTools.Count);
                Assert.Contains(discovery.DiscoveredTools, tool => tool.DisplayName == "OpenCV/Blob");
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData("开始", typeof(StartNode))]
    [InlineData("结束", typeof(EndNode))]
    [InlineData("延时", typeof(DelayNode))]
    [InlineData("日志输出", typeof(LogNode))]
    [InlineData("条件分发", typeof(SwitchNode))]
    [InlineData("循环", typeof(LoopNode))]
    [InlineData("分支汇聚", typeof(BranchMergeNode))]
    public async Task BuiltinPalette_CreatesOriginalIndependentTypes_AndHonorsModificationLock(string name, Type type)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var item = Items(vm).Single(item => item.DisplayName == name);
                var first = vm.AddNodePaletteAt(item)!;
                var second = vm.AddNodePaletteAt(item, new Point(1400, 900))!;
                Assert.Equal(type, first.GetType());
                Assert.Equal(type, second.GetType());
                Assert.NotSame(first, second);
                Assert.NotEqual(first.Id, second.Id);
                Assert.Equal(1400, second.X);
                Assert.Same(second, vm.Chart.SelectedNode!.Model);
                Assert.Same(second, vm.PropertyGridVM.TargetObject);
                Assert.Null(vm.SelectedEditor);
                Assert.True(vm.Chart.Model.IsModified);
                if (first is StartNode) Assert.False(first.IsInEnabled);
                if (first is EndNode) Assert.False(first.IsOutEnabled);
                if (first is SwitchNode dispatch) Assert.True(dispatch.RequireInputConnection);
                vm.Chart.Model.IsRunning = true;
                Assert.False(vm.AddPaletteCommand.CanExecute(item));
                Assert.Null(vm.AddNodePaletteAt(item));
                Assert.Equal(2, vm.Chart.Nodes.Count);
                vm.Chart.Model.IsRunning = false;
            });
        }
        finally
        {
            host.Invoke(() => vm.Chart.Model.IsRunning = false);
            await host.Invoke(() => vm.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task MixedFlow_UsesOriginalExecutionAndDocumentFactories_AfterReopen()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        var path = Path.Combine(Path.GetTempPath(), $"vision-registration-{Guid.NewGuid():N}.flow");
        string[] ids = [];
        try
        {
            host.Invoke(() =>
            {
                NodeModel Add(string name) => vm.AddNodePaletteAt(Items(vm).Single(item => item.DisplayName == name))!;
                var start = Add("开始");
                var delay = Add("延时");
                var statistics = Assert.IsAssignableFrom<VisionToolNodeBase>(Add("OpenCV/GrayStatistics"));
                var end = Add("结束");
                vm.SetImage(VisionImage.FromBuffer(4, 4, PixelFormat.Mono8, Enumerable.Repeat((byte)73, 16).ToArray()));
                Wire(vm, start, delay); Wire(vm, delay, statistics); Wire(vm, statistics, end);
                ids = vm.Chart.Nodes.Select(node => node.Id).ToArray();
                Assert.True(vm.Chart.SaveDocument(path));
                Assert.True(reopened.Chart.LoadDocument(path));
                Assert.Equal(ids, reopened.Chart.Nodes.Select(node => node.Id));
                Assert.Equal(new[] { typeof(StartNode), typeof(DelayNode), typeof(Vision.FlowNode.OpenCv.GrayStatisticsNode), typeof(EndNode) },
                    reopened.Chart.Nodes.Select(node => node.Model.GetType()));
                Assert.Equal(3, reopened.Chart.Connections.Count);
                Assert.NotSame(statistics.Session.Parameters, reopened.Chart.Nodes.Select(node => node.Model).OfType<VisionToolNodeBase>().Single().Session.Parameters);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var tool = reopened.Chart.Nodes.Select(node => node.Model).OfType<VisionToolNodeBase>().Single();
                Assert.Equal(73d, Assert.IsType<GrayStatisticsResult>(tool.Result).Mean);
                Assert.All(reopened.Chart.Nodes, node => Assert.Equal(FlowEditor.Core.Nodes.NodeStatus.Success, node.Model.Status));
            });
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            await host.Invoke(() => reopened.DisposeAsync().AsTask());
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OtherHosts_CanOptIntoSelectedVisionMetadata_WithoutRegisteringVisionPage(bool includeVision)
    {
        host.Invoke(() =>
        {
            var created = 0;
            var entries = OpenCvToolCatalog.Entries.Select(entry => entry with
            {
                Create = () => { created++; return entry.Create(); }
            }).ToArray();
            var catalog = new VisionBrowserCatalog(entries, [], _ => throw new NotSupportedException(), () => null);
            var nodes = new FlowNodeCatalog([typeof(NodeModel).Assembly, typeof(VisionToolNodeBase).Assembly]);
            var baseline = new FlowToolDiscoveryService(nodes);
            var registrations = includeVision
                ? VisionFlowRegistration.CreateToolRegistrations(catalog).Where(tool => tool.ToolId is "OpenCV/Blob" or "OpenCV/FindLine").ToArray()
                : [];
            var combined = new FlowToolDiscoveryService(nodes, registrations);
            Assert.Equal(baseline.Discover() + (includeVision ? 2 : 0), combined.Discover());
            Assert.Equal(includeVision, combined.DiscoveredTools.Any(tool => tool.ToolId == "OpenCV/Blob"));
            Assert.DoesNotContain(combined.DiscoveredTools, tool => tool.ToolId == "OpenCV/Crop");
            Assert.Equal(typeof(Vision.FlowNode.OpenCv.BlobNode), nodes.TryResolveType("vision.opencv.blob", null));
            Assert.Equal(0, created);
        });
    }

    [Fact]
    public async Task LoopBody_ReusesOwnedVisionSession_ForEveryIteration()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        var results = new HashSet<Vision.Core.Results.ToolResult>();
        VisionToolNodeBase statistics = null!;
        LoopNode loop = null!;
        EventHandler changed = (_, _) => { if (statistics.Result is { } result) results.Add(result); };
        try
        {
            host.Invoke(() =>
            {
                NodeModel Add(string name) => vm.AddNodePaletteAt(Items(vm).Single(item => item.DisplayName == name))!;
                var start = Add("开始"); loop = Assert.IsType<LoopNode>(Add("循环")); loop.Iterations = 3;
                statistics = Assert.IsAssignableFrom<VisionToolNodeBase>(Add("OpenCV/GrayStatistics"));
                var end = Add("结束");
                vm.SetImage(VisionImage.FromBuffer(4, 4, PixelFormat.Mono8, Enumerable.Repeat((byte)41, 16).ToArray()));
                Wire(vm, start, loop);
                Wire(vm, loop, statistics, "Body"); Wire(vm, loop, end, "Completed");
                statistics.Session.StateChanged += changed;
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(3, results.Count);
                Assert.Equal(0, loop.CurrentIndex);
                Assert.True(vm.Owns(statistics));
                Assert.Equal(41d, Assert.IsType<GrayStatisticsResult>(statistics.Result).Mean);
            });
        }
        finally
        {
            host.Invoke(() => { if (statistics != null) statistics.Session.StateChanged -= changed; });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task StopDuringBuiltinDelay_LocksPalette_AndDoesNotExecuteDownstreamVision()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DelayNode delay = null!;
        VisionToolNodeBase statistics = null!;
        Task execution = Task.CompletedTask;
        System.ComponentModel.PropertyChangedEventHandler changed = (_, e) =>
        {
            if (e.PropertyName == nameof(NodeModel.Status) && delay.Status == FlowEditor.Core.Nodes.NodeStatus.Running)
                started.TrySetResult();
        };
        try
        {
            host.Invoke(() =>
            {
                NodeModel Add(string name) => vm.AddNodePaletteAt(Items(vm).Single(item => item.DisplayName == name))!;
                var start = Add("开始"); delay = Assert.IsType<DelayNode>(Add("延时")); delay.DelayMs = 60_000;
                statistics = Assert.IsAssignableFrom<VisionToolNodeBase>(Add("OpenCV/GrayStatistics"));
                Wire(vm, start, delay); Wire(vm, delay, statistics);
                delay.PropertyChanged += changed;
                execution = vm.RunFlowCommand.ExecuteAsync(null);
            });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                Assert.False(vm.AddPaletteCommand.CanExecute(Items(vm).First()));
                vm.Chart.StopCommand.Execute(null);
            });
            await execution.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                Assert.False(vm.Chart.Model.IsRunning);
                Assert.Null(statistics.Result);
                Assert.True(vm.AddPaletteCommand.CanExecute(Items(vm).First()));
            });
        }
        finally
        {
            host.Invoke(() => { if (delay != null) delay.PropertyChanged -= changed; vm.Chart.StopCommand.Execute(null); });
            await execution.WaitAsync(TimeSpan.FromSeconds(10));
            await host.Invoke(() => vm.DisposeAsync().AsTask());
        }
    }

    private static IEnumerable<NodePaletteItem> Items(VisionFlowViewModel vm) =>
        vm.ToolboxCategories.SelectMany(category => category.Items.Concat(category.SubGroups.SelectMany(group => group.Items)));

    private static void Wire(VisionFlowViewModel vm, NodeModel source, NodeModel target, string output = "Next_Out") => vm.Chart.Connections.Add(new()
    {
        SourceNodeId = source.Id,
        SourcePortId = source.OutputPorts.Single(port => port.Name == output).Id,
        TargetNodeId = target.Id,
        TargetPortId = target.InputPorts.Single(port => port.Name == "Next_IN").Id
    });
}
