using FlowEditor.Controls.Nodes;
using FlowEditor.Core;
using FlowEditor.Core.Models;
using FlowEditor.Runtime;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Core.Tools;
using Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Trait("Category", "Contract")]
public sealed class VisionToolFlowNodeTests
{
    [Fact]
    public async Task ExistingEngine_RunsIndependentTools_WithFixturePortAndOriginalParameters()
    {
        var image = VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 1600).ToArray());
        var firstTool = new BlobTool();
        firstTool.Parameters.MinGray = 128;
        firstTool.Parameters.Features = [];
        var secondTool = new BlobTool();
        secondTool.Parameters.MaxGray = 0;
        secondTool.Parameters.Features = [];
        var parameters = firstTool.Parameters;
        var baseline = Roi.Rectangle(10, 10, 4, 4);
        var locator = new FixtureSource { Pose = new(20, 0, 0), IsInEnabled = false };
        using var first = new VisionToolFlowNode(firstTool)
        {
            Image = image, SearchRoi = baseline, ReferenceFixture = new(10, 0, 0)
        };
        using var second = new VisionToolFlowNode(secondTool) { Image = image };
        var poseOut = locator.AddCustomOutputPort("位姿", nameof(FixtureSource.Pose));
        var poseIn = first.AddCustomInputPort("定位", nameof(VisionToolFlowNode.Fixture));
        NodeModel[] nodes = [locator, first, second];
        ConnectionModel[] edges =
        [
            Connect(locator, poseOut.Id, first, poseIn.Id),
            Connect(first, first.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                second, second.InputPorts.Single(p => p.Name == "Next_IN").Id)
        ];
        var engine = new FlowChartExecutionEngine();
        await engine.ExecuteAsync(nodes, edges, CancellationToken.None, _ => { });
        var item = Assert.Single(Assert.IsType<BlobResult>(first.Result).Items);
        Assert.Equal(16, item.Area);
        Assert.Equal(new Rect2D(18, 8, 4, 4), item.Bounds);
        Assert.Empty(Assert.IsType<BlobResult>(second.Result).Items);
        Assert.Same(parameters, firstTool.Parameters);
        Assert.NotSame(firstTool.Parameters, secondTool.Parameters);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(new Pose2D(10, 10, 0), baseline.Pose);
        Assert.Null(firstTool.SearchRegion);
        Assert.True(first.RunSequence < second.RunSequence);
        Assert.All(image.Pixels, p => Assert.Equal(255, p));

        await engine.ExecuteAsync(nodes, edges, CancellationToken.None, _ => { });
        Assert.Equal(item.Bounds, Assert.Single(Assert.IsType<BlobResult>(first.Result).Items).Bounds);
        Assert.Equal(new Pose2D(10, 10, 0), baseline.Pose); // 同一帧重跑不累计跟随量。
    }

    [Theory]
    [InlineData(0, 0, 90)]
    [InlineData(5, -3, 0)]
    public async Task RuntimeRegion_FollowsRigidPose_AndRestoresOriginalRegion(double x, double y, double angle)
    {
        var original = new SearchRegion(Roi.Rectangle(1, 1, 2, 2));
        var baseline = Roi.Rectangle(10, 0, 4, 2);
        SearchRegion? observed = null;
        var tool = new ProbeTool { SearchRegion = original };
        tool.Execute = () => { observed = tool.SearchRegion; return new ToolResult(); };
        using var node = new VisionToolFlowNode(tool) { SearchRoi = baseline, Fixture = new(x, y, angle) };
        await node.ExecuteAsync(CancellationToken.None);
        Assert.Same(original, tool.SearchRegion);
        var expectedCenter = new Pose2D(x, y, angle).Transform(new Point2D(10, 0));
        Assert.NotNull(observed);
        Assert.True(observed.Contains(expectedCenter.X, expectedCenter.Y));
        Assert.False(observed.Contains(100, 100));
        if (angle == 90)
        {
            Assert.True(observed.Contains(0, 11.5));
            Assert.False(observed.Contains(1.5, 10));
        }
        Assert.Equal(new Pose2D(10, 0, 0), baseline.Pose);
    }

    [Fact]
    public async Task Failure_ClearsOldPort_DisposesPreviousResult_AndRestoresRegion()
    {
        var previous = new TrackedResult();
        var tool = new ProbeTool { Execute = () => previous };
        using var node = new VisionToolFlowNode(tool) { SearchRoi = Roi.Rectangle(10, 10, 4, 4) };
        var output = node.AddCustomOutputPort("结果", nameof(VisionToolFlowNode.Result));
        await node.ExecuteAsync(CancellationToken.None);
        node.SyncOutputValues();
        Assert.Same(previous, output.Value);
        tool.Execute = () => new ToolResult { Success = false, Message = "失败证据" };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Contains("失败证据", error.Message);
        Assert.Equal(1, previous.DisposeCount);
        Assert.Null(output.Value);
        Assert.Null(tool.SearchRegion);
    }

    [Fact]
    public async Task EngineFailure_DoesNotRunDownstream()
    {
        using var first = new VisionToolFlowNode(new ProbeTool
        { Execute = () => new ToolResult { Success = false, Message = "失败" } }) { IsInEnabled = false };
        var secondTool = new ProbeTool();
        using var second = new VisionToolFlowNode(secondTool);
        var edge = Connect(first, first.OutputPorts.Single().Id, second, second.InputPorts.Single().Id);
        await Assert.ThrowsAnyAsync<Exception>(() => new FlowChartExecutionEngine().ExecuteAsync(
            new NodeModel[] { first, second }, new[] { edge }, CancellationToken.None, _ => { }));
        Assert.Equal(0, secondTool.RunCount);
        Assert.Null(second.Result);
    }

    [Fact]
    public async Task CancellationAfterSynchronousCall_DiscardsResult_WithoutPretendingSdkWasInterrupted()
    {
        using var cancellation = new CancellationTokenSource();
        var result = new TrackedResult();
        var tool = new ProbeTool { Execute = () => { cancellation.Cancel(); return result; } };
        using var node = new VisionToolFlowNode(tool) { SearchRoi = Roi.Circle(5, 5, 4) };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => node.ExecuteAsync(cancellation.Token));
        Assert.Null(node.Result);
        Assert.Equal(1, result.DisposeCount);
        Assert.Null(tool.SearchRegion);
        Assert.Equal(1, tool.RunCount);
    }

    [Fact]
    public async Task DisposeDuringCall_DefersToolRelease_AndRejectsLateResultAndReentry()
    {
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var result = new TrackedResult();
        var tool = new ProbeTool { Execute = () =>
        {
            entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException();
            return result;
        } };
        var node = new VisionToolFlowNode(tool);
        var run = Task.Run(() => node.ExecuteAsync(CancellationToken.None));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
            node.Dispose();
            node.Dispose();
            Assert.Equal(0, tool.DisposeCount);
        }
        finally { resume.Set(); }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => run.WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.Null(node.Result);
        Assert.Equal(1, result.DisposeCount);
        Assert.Equal(1, tool.DisposeCount);
        node.Dispose();
        Assert.Equal(1, tool.DisposeCount);
    }

    [Fact]
    public async Task InvalidFixture_DoesNotRunOrModifyBaseline()
    {
        var tool = new ProbeTool();
        var baseline = Roi.Rectangle(10, 10, 4, 4);
        using var node = new VisionToolFlowNode(tool) { SearchRoi = baseline, Fixture = new(double.NaN, 0, 0) };
        await Assert.ThrowsAsync<ArgumentException>(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Equal(0, tool.RunCount);
        Assert.Null(tool.SearchRegion);
        Assert.Equal(new Pose2D(10, 10, 0), baseline.Pose);
    }

    [Fact]
    public async Task ToolWithoutSearchCapability_RejectsFixtureRatherThanPretendingToFollow()
    {
        using var node = new VisionToolFlowNode(new Vision.Tools.OpenCv.Processing.ThresholdTool())
        { SearchRoi = Roi.Rectangle(1, 1, 2, 2), Fixture = new(1, 1, 0) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Null(node.Result);
    }

    private static ConnectionModel Connect(NodeModel source, string sourcePort, NodeModel target, string targetPort) => new()
    { SourceNodeId = source.Id, SourcePortId = sourcePort, TargetNodeId = target.Id, TargetPortId = targetPort };

    private sealed class FixtureSource : NodeModel
    {
        [NodeOutput] public Pose2D Pose { get; set; }
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ProbeTool : IImageTool, ISearchRegionTool
    {
        public string Name => "受控离线工具";
        public long LastElapsedMs => 0;
        public SearchRegion? SearchRegion { get; set; }
        public Func<ToolResult> Execute { get; set; } = () => new ToolResult();
        public int RunCount { get; private set; }
        public int DisposeCount { get; private set; }
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) { RunCount++; return Execute(); }
        public void Dispose() => DisposeCount++;
    }

    private sealed class TrackedResult : ToolResult
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }
}
