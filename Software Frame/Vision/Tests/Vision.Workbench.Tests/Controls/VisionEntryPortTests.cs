using System.IO;
using FlowEditor.Controls.Nodes.FlowControl;
using Vision.Core.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionEntryPortTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task FirstTool_CanRunAlone_AndLaterConnectFromStart_ThenSaveAndReopen()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        var restored = host.Invoke(VisionFlowToolboxTests.Create);
        var path = Path.Combine(Path.GetTempPath(), $"vision-entry-{Guid.NewGuid():N}.flow");
        VisionToolNodeBase tool = null!;
        try
        {
            host.Invoke(() =>
            {
                tool = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                Assert.True(tool.IsInEnabled);
                Assert.Single(tool.InputPorts, p => p.Name == "Next_IN");
                vm.SetImage(VisionImage.FromBuffer(4, 4, PixelFormat.Mono8, Enumerable.Repeat((byte)73, 16).ToArray()));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(73d, Assert.IsType<GrayStatisticsResult>(tool.Result).Mean));
            host.Invoke(() =>
            {
                var palette = vm.ToolboxCategories.SelectMany(c => c.Items).Single(p => p.DisplayName == "开始");
                var start = Assert.IsType<StartNode>(vm.AddNodePaletteAt(palette));
                Assert.False(start.IsInEnabled);
                vm.Chart.Connections.Add(new()
                {
                    SourceNodeId = start.Id, SourcePortId = start.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                    TargetNodeId = tool.Id, TargetPortId = tool.InputPorts.Single(p => p.Name == "Next_IN").Id
                });
                Assert.True(vm.Chart.SaveDocument(path)); Assert.True(restored.Chart.LoadDocument(path));
                Assert.True(restored.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single().IsInEnabled);
                Assert.Single(restored.Chart.Connections);
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var start = restored.Chart.Nodes.Select(n => n.Model).OfType<StartNode>().Single();
                var node = restored.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single();
                Assert.Equal(73d, Assert.IsType<GrayStatisticsResult>(node.Result).Mean);
                Assert.True(start.RunSequence < node.RunSequence);
            });
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            await host.Invoke(() => restored.DisposeAsync().AsTask());
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task LaterUnconnectedTool_StillRequiresFlowInput()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var entry = vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics");
                var first = vm.Add(entry); var second = vm.Add(entry);
                Assert.True(first.IsInEnabled); Assert.True(second.IsInEnabled);
                Assert.False(((FlowEditor.Core.IFlowValidationNode)first).RequiresFlowInput);
                Assert.True(((FlowEditor.Core.IFlowValidationNode)second).RequiresFlowInput);
                var report = FlowEditor.Runtime.FlowValidator.Analyze(vm.Chart.Nodes.Select(n => (FlowEditor.Core.INodeExecutable)n.Model).ToList(), vm.Chart.Connections.ToList());
                Assert.Single(report.Diagnostics, d => d.Code == "E-IN-REQ");
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task FirstSwitch_KeepsOriginalNextInput_AndStrictDataInput()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var palette = vm.ToolboxCategories.SelectMany(c => c.Items).Single(p => p.DisplayName == "条件分发");
                var node = Assert.IsType<SwitchNode>(vm.AddNodePaletteAt(palette));
                Assert.True(node.IsInEnabled); Assert.Single(node.InputPorts, p => p.Name == "Next_IN");
                Assert.True(node.RequireInputConnection);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacyDisabledInput_RemainsDisabledWhenReopened()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        var restored = host.Invoke(VisionFlowToolboxTests.Create);
        var path = Path.Combine(Path.GetTempPath(), $"vision-entry-legacy-{Guid.NewGuid():N}.flow");
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                node.IsInEnabled = false;
                Assert.True(vm.Chart.SaveDocument(path)); Assert.True(restored.Chart.LoadDocument(path));
                var loaded = restored.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single();
                Assert.False(loaded.IsInEnabled); Assert.DoesNotContain(loaded.InputPorts, p => p.Name == "Next_IN");
            });
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask());
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
