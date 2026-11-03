using Vision.Controls.Services;
using Vision.Core.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionExecutionImageTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualAndFlowExecutions_ChooseTheirOwnInputs_WithoutReplacingSession(bool connected)
    {
        var local = Image(201);
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create(null, catalog => catalog with { LoadImage = () => local }));
        VisionToolNodeBase target = null!;
        VisionToolInstanceWorkspace workspace = null!;
        try
        {
            host.Invoke(() =>
            {
                if (connected)
                {
                    var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Threshold"));
                    target = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                    vm.Chart.Connections.Add(new()
                    {
                        SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                        TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == "Next_IN").Id
                    });
                    VisionImageBinding.Select(target, source);
                }
                else target = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                vm.SetImage(Image(73));
                workspace = (VisionToolInstanceWorkspace)target.Session;
                workspace.LoadImageCommand.Execute(null);
                Assert.Equal(201, workspace.ConfiguredImage!.Pixels[0]);
            });
            await host.Invoke(() => workspace.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(201d, Assert.IsType<GrayStatisticsResult>(target.Result).Mean));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(connected ? 255d : 73d, Assert.IsType<GrayStatisticsResult>(target.Result).Mean);
                Assert.Equal(201, workspace.ConfiguredImage!.Pixels[0]);
                Assert.Same(workspace, target.Session);
            });
            await host.Invoke(() => workspace.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(201d, Assert.IsType<GrayStatisticsResult>(target.Result).Mean));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ChangingTaskInputAndReopeningFlow_PreserveLocalInputAndBothExecutionEntrypoints()
    {
        var local = Image(201);
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create(null, catalog => catalog with { LoadImage = () => local }));
        var restored = host.Invoke(VisionFlowToolboxTests.Create);
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vision-inputs-{Guid.NewGuid():N}.flow");
        VisionToolNodeBase target = null!;
        VisionToolInstanceWorkspace workspace = null!;
        try
        {
            host.Invoke(() =>
            {
                target = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                vm.SetImage(Image(73)); workspace = (VisionToolInstanceWorkspace)target.Session;
                workspace.LoadImageCommand.Execute(null);
                vm.SetImage(Image(92));
                Assert.Equal(201, workspace.ConfiguredImage!.Pixels[0]);
                Assert.Equal(92, workspace.FlowInputImage!.Pixels[0]);
                // 后续新增节点应继承任务图，不继承第一个工具的本地导图。
                var next = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                var third = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                Assert.Equal(92, next.Session.ConfiguredImage!.Pixels[0]); Assert.Equal(92, third.Session.ConfiguredImage!.Pixels[0]);
                next.IsExcludedFromExecution = true; third.IsExcludedFromExecution = true;
                Assert.True(vm.Chart.SaveDocument(path)); Assert.True(restored.Chart.LoadDocument(path));
                target = restored.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().First();
                workspace = (VisionToolInstanceWorkspace)target.Session;
                Assert.True(workspace.ExportConfiguration().UseLocalInputImage);
                Assert.Equal(201, workspace.ConfiguredImage!.Pixels[0]);
            });
            await host.Invoke(() => workspace.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(201d, Assert.IsType<GrayStatisticsResult>(target.Result).Mean));
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(92d, Assert.IsType<GrayStatisticsResult>(target.Result).Mean));
            await host.Invoke(() => workspace.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(201d, Assert.IsType<GrayStatisticsResult>(target.Result).Mean));
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask());
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    [Fact]
    public async Task MissingFlowImage_DoesNotFallBackToToolLocalImport()
    {
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create(null, catalog => catalog with { LoadImage = () => Image(201) }));
        VisionToolNodeBase target = null!;
        VisionToolInstanceWorkspace workspace = null!;
        try
        {
            host.Invoke(() =>
            {
                target = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                workspace = (VisionToolInstanceWorkspace)target.Session; workspace.LoadImageCommand.Execute(null);
                Assert.Null(workspace.FlowInputImage);
            });
            await host.Invoke(() => workspace.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(201d, Assert.IsType<GrayStatisticsResult>(target.Result).Mean));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.False(target.Result?.Success == true);
                Assert.Null(workspace.Image); Assert.Equal(201, workspace.ConfiguredImage!.Pixels[0]);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    private static VisionImage Image(byte gray) => VisionImage.FromBuffer(4, 4, PixelFormat.Mono8, Enumerable.Repeat(gray, 16).ToArray());
}
