using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Tools;
using Vision.Tools.Execution;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class BlobWorkflowTests
{
    [Fact]
    public async Task ActualTemplate_FollowedRegion_Blob_AndNoImageCountAreaJudgments_FormOneWorkflow()
    {
        var (template, scene) = Scene();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var locating = await engine.CreateContextAsync(default);
        var inspecting = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(locating, new TemplateModelDefinition(template, new(0, 0), new()), 1, default);
        var found = await engine.ExecuteAsync(locating, new TemplateMatchOperation(scene, model, new(), 0.99), default);
        Assert.Equal(VisionExecutionStatus.Success, found.Status);
        Assert.Single(found.Value!.Matches);
        var reference = new SearchRegion(Roi.Rectangle(15, 3, 12, 6));
        var binding = VisionRegionBinding.Follow(reference, found.Value, model, 0);
        var request = new BlobOperation(scene, 255, 255, binding);
        Assert.Null(request.Model);
        var result = await engine.ExecuteAsync(inspecting, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var blobs = result.Value!;
        Assert.Equal(new long[] { 6, 4 }, blobs.Items.Select(item => item.Area));
        Assert.Equal(new Point2D(21, 7.5), blobs.Items[0].Center);
        Assert.Equal(scene.FrameId, blobs.FrameId);
        Assert.Equal(scene.CoordinateSpaceId, blobs.CoordinateSpaceId);
        Assert.True(reference.Contains(15, 3));
        Assert.NotEmpty(blobs.Items[0].Contours);
        var countRequest = BlobJudgmentOperation.ForCount(blobs, 2, 2);
        Assert.Null(countRequest.Image);
        var count = await engine.ExecuteAsync(inspecting, countRequest, default);
        Assert.Equal(VisionExecutionStatus.Success, count.Status);
        Assert.True(count.Value!.Passed);
        var ng = await engine.ExecuteAsync(inspecting, BlobJudgmentOperation.ForCount(blobs, 3, 5), default);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
        var area = await engine.ExecuteAsync(inspecting, BlobJudgmentOperation.ForArea(blobs, 1, 5, 6), default);
        Assert.Equal(VisionExecutionStatus.Success, area.Status);
        Assert.True(area.Value!.Passed);
        Assert.Equal(6, area.Value.ActualValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => BlobJudgmentOperation.ForArea(blobs, 0, 0, 10));
        await engine.ReleaseContextAsync(locating, default);
        await engine.ReleaseContextAsync(inspecting, default);
        var evaluating = await engine.CreateContextAsync(default);
        var closedSource = await engine.ExecuteAsync(evaluating, request, default);
        Assert.Equal(VisionExecutionStatus.Failed, closedSource.Status);
        Assert.Equal("LocationModelNotCurrent", closedSource.Issue!.Code);
        var snapshot = await engine.ExecuteAsync(evaluating, BlobJudgmentOperation.ForPresence(blobs), default);
        Assert.True(snapshot.Value!.Passed);
        Assert.NotEmpty(blobs.Items[0].Contours[0].Points);
    }

    [Fact]
    public async Task Following_RejectsOtherFrameSpace_RebuiltResults_AndRetiredModelRevision()
    {
        var (template, scene) = Scene();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var locating = await engine.CreateContextAsync(default);
        var inspecting = await engine.CreateContextAsync(default);
        var definition = new TemplateModelDefinition(template, new(), new());
        var model = await engine.PrepareModelAsync(locating, definition, 1, default);
        var found = await engine.ExecuteAsync(locating, new TemplateMatchOperation(scene, model, new(), 0.99), default);
        Assert.Equal(VisionExecutionStatus.Success, found.Status);
        var reference = new SearchRegion(Roi.Rectangle(15, 3, 12, 6));
        var binding = VisionRegionBinding.Follow(reference, found.Value!, model, 0);
        var wrongFrame = new VisionFrame(Guid.NewGuid(), scene.CoordinateSpaceId, scene.Width, scene.Height, scene.Format, scene.Pixels);
        var wrongSpace = new VisionFrame(scene.FrameId, Guid.NewGuid(), scene.Width, scene.Height, scene.Format, scene.Pixels);
        Assert.Throws<ArgumentException>(() => new BlobOperation(wrongFrame, 255, 255, binding));
        Assert.Throws<ArgumentException>(() => new BlobOperation(wrongSpace, 255, 255, binding));
        var relabeledFrame = new VisionFrame(scene.FrameId, scene.CoordinateSpaceId, scene.Width, scene.Height, scene.Format, scene.Pixels);
        var wrongBranch = await engine.ExecuteAsync(inspecting, new BlobOperation(relabeledFrame, 255, 255, binding), default);
        Assert.Equal(VisionExecutionStatus.Failed, wrongBranch.Status);
        Assert.Equal("LocationFrameMismatch", wrongBranch.Issue!.Code);
        var rebuilt = new TemplateMatchOutput(scene.FrameId, scene.CoordinateSpaceId, found.Value!.ScoreMeaning, found.Value.Matches);
        var unregistered = await engine.ExecuteAsync(inspecting, new BlobOperation(scene, 255, 255,
            VisionRegionBinding.Follow(reference, rebuilt, model, 0)), default);
        Assert.Equal(VisionExecutionStatus.Failed, unregistered.Status);
        Assert.Equal("LocationNotPublished", unregistered.Issue!.Code);
        var stale = new BlobOperation(scene, 255, 255, binding);
        await engine.PrepareModelAsync(locating, definition, 2, default);
        var rejected = await engine.ExecuteAsync(inspecting, stale, default);
        Assert.Equal(VisionExecutionStatus.Failed, rejected.Status);
        Assert.Null(rejected.Value);
        Assert.Equal("LocationModelNotCurrent", rejected.Issue!.Code);
    }

    [Theory]
    [InlineData(BlobConnectivity.Four, 2)]
    [InlineData(BlobConnectivity.Eight, 1)]
    public async Task Connectivity_IsExplicit_AndDegeneratePixelContoursRemainReadable(BlobConnectivity connectivity, int expected)
    {
        var frame = Gray(3, 3, [255, 0, 0, 0, 255, 0, 0, 0, 0]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(frame, 255, 255, connectivity: connectivity), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Items.Count);
        Assert.Equal(2, result.Value.Items.Sum(item => item.Area));
        Assert.All(result.Value.Items, item => Assert.NotEmpty(item.Contours));
    }

    [Theory]
    [InlineData(BlobHolePolicy.Preserve, 16, 1)]
    [InlineData(BlobHolePolicy.Fill, 25, 0)]
    public async Task HolePolicy_ChangesPixelAreaAndContours_WithoutChangingSource(BlobHolePolicy policy, long area, int holes)
    {
        var pixels = new byte[49];
        for (var y = 1; y <= 5; y++)
        {
            for (var x = 1; x <= 5; x++)
            {
                if (x == 1 || x == 5 || y == 1 || y == 5)
                {
                    pixels[y * 7 + x] = 255;
                }
            }
        }
        var frame = Gray(7, 7, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(frame, 255, 255, holePolicy: policy), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(area, item.Area);
        Assert.Equal(holes, item.HoleCount);
        Assert.Equal(area / 25d, item.Extent);
        Assert.Equal(new Point2D(3, 3), item.Center);
        Assert.Equal(pixels, frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData(BlobFilterLogic.All, 0)]
    [InlineData(BlobFilterLogic.Any, 2)]
    public async Task FeatureLogicAndSorting_AreReal_AndEmptyCountCanPass(BlobFilterLogic logic, int expected)
    {
        var pixels = new byte[12 * 8];
        Rect(pixels, 12, 1, 1, 2, 2);
        Rect(pixels, 12, 6, 1, 3, 3);
        pixels[6 * 12 + 10] = 255;
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var source = Gray(12, 8, pixels);
        var request = new BlobOperation(source, 255, 255,
            filters: [new(BlobFeature.Area, 5, 10), new(BlobFeature.Width, 1, 1)],
            filterLogic: logic, sort: BlobSort.AreaAscending);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(expected == 0 ? VisionExecutionStatus.NoMatch : VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Items.Count);
        if (expected == 2)
        {
            Assert.Equal(new long[] { 1, 9 }, result.Value.Items.Select(item => item.Area));
            Assert.Equal(new[] { 1, 2 }, result.Value.Items.Select(item => item.Index));
        }
        var judgment = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForCount(result.Value, expected, expected), default);
        Assert.Equal(VisionExecutionStatus.Success, judgment.Status);
        Assert.True(judgment.Value!.Passed);
    }

    [Fact]
    public async Task MaximumResults_IsAFailureBudget_NotAnUnreportedTopNCount()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context,
            new BlobOperation(Gray(3, 1, [255, 0, 255]), 255, 255, maximumResults: 1), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void FrozenRegion_UsesNonCentralAnchorRotationAndScale_WithoutAccumulation()
    {
        var frame = Gray(8, 8, new byte[64]);
        var model = new VisionModelReference(new(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(), "test",
            new TemplateModelDefinition(frame, new(3, 2), new(0, 360, 1, 2)), 1);
        var candidate = new TemplateMatchCandidate(new(100, 80), new(3, 2), 90, 2, 0.9);
        var found = new TemplateMatchOutput(frame.FrameId, frame.CoordinateSpaceId, "geometry only", [candidate]);
        var reference = new SearchRegion(Roi.Rectangle(5, 2, 2, 2));
        var binding = VisionRegionBinding.Follow(reference, found, model, 0);
        var first = binding.Resolve(frame);
        var second = binding.Resolve(frame);
        Assert.True(first.Contains(100, 84));
        Assert.False(first.Contains(100, 90));
        Assert.Equal(first.Bounds, second.Bounds);
        Assert.True(reference.Contains(5, 2));
    }

    [Fact]
    public async Task NoImageJudgment_UsesCommonImplementation_NotTheSelectedSdkBackend()
    {
        var backend = new NoExecutionBackend();
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var source = new BlobOutput(Guid.NewGuid(), Guid.NewGuid(), []);
        var result = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForCount(source, 0, 0), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.True(result.Value!.Passed);
        var absent = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForPresence(source, false), default);
        var present = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForPresence(source), default);
        Assert.True(absent.Value!.Passed);
        Assert.False(present.Value!.Passed);
    }

    [Fact]
    public async Task Mono16AndFixedRegion_KeepOriginalThresholdDomain_AndIgnoreRowPadding()
    {
        var pixels = new byte[36];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(0, 2), 1000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(16, 2), 1000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(18, 2), 1100);
        for (var row = 0; row < 3; row++)
        {
            pixels.AsSpan(row * 12 + 8, 4).Fill(255);
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 4, 3, PixelFormat.Mono16, pixels, 12);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var region = new VisionRegionBinding(new SearchRegion(Roi.Rectangle(2.5, 1.5, 2, 2)));
        var result = await engine.ExecuteAsync(context, new BlobOperation(frame, 1000, 1100, region), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(2, item.Area);
        Assert.Equal(new Point2D(2.5, 1), item.Center);
        Assert.Equal(new Rect2D(2, 1, 2, 1), item.Bounds);
    }

    [Fact]
    public async Task MultipleMatches_FollowTheExplicitCandidate_NotImplicitFirstTarget()
    {
        var (template, single) = Scene();
        var pixels = new byte[80 * 24];
        for (var row = 0; row < 24; row++)
        {
            single.Pixels.Slice(row * 40, 40).CopyTo(pixels.AsSpan(row * 80, 40));
            single.Pixels.Slice(row * 40, 40).CopyTo(pixels.AsSpan(row * 80 + 40, 40));
        }
        var scene = Gray(80, 24, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new TemplateModelDefinition(template, new(), new()), 1, default);
        var found = await engine.ExecuteAsync(context,
            new TemplateMatchOperation(scene, model, new(), 0.99, maximumMatches: 2), default);
        Assert.Equal(VisionExecutionStatus.Success, found.Status);
        Assert.Equal(2, found.Value!.Matches.Count);
        var selected = found.Value.Matches[1];
        var binding = VisionRegionBinding.Follow(new SearchRegion(Roi.Rectangle(15, 3, 12, 6)), found.Value, model, 1);
        var blobs = await engine.ExecuteAsync(context, new BlobOperation(scene, 255, 255, binding), default);
        Assert.Equal(VisionExecutionStatus.Success, blobs.Status);
        Assert.Equal(new long[] { 6, 4 }, blobs.Value!.Items.Select(item => item.Area));
        Assert.Equal(selected.Position.X + 17, blobs.Value.Items[0].Center.X, 8);
    }

    [Fact]
    public async Task SourceModelReplacedDuringOtherContextExecution_DoesNotPublishLateDerivedResult()
    {
        var backend = new PausingBlobBackend();
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => backend);
        var locating = await engine.CreateContextAsync(default);
        var inspecting = await engine.CreateContextAsync(default);
        var (template, scene) = Scene();
        var definition = new TemplateModelDefinition(template, new(), new());
        var model = await engine.PrepareModelAsync(locating, definition, 1, default);
        var found = await engine.ExecuteAsync(locating, new TemplateMatchOperation(scene, model, new()), default);
        var binding = VisionRegionBinding.Follow(new SearchRegion(Roi.Rectangle(15, 3, 12, 6)), found.Value!, model, 0);
        var pending = engine.ExecuteAsync(inspecting, new BlobOperation(scene, 255, 255, binding), default).AsTask();
        try
        {
            await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await engine.PrepareModelAsync(locating, definition, 2, default);
        }
        finally
        {
            backend.Resume.TrySetResult();
        }
        var result = await pending;
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Equal("LocationModelNotCurrent", result.Issue!.Code);
    }

    // 只验证跨上下文发布事务，不充当真实Blob算法或硬件验收证据。
    private sealed class PausingBlobBackend : IVisionBackend
    {
        public TaskCompletionSource Entered
        {
            get;
        } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume
        {
            get;
        } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities
        {
            get;
        } =
        [
            new(TemplateMatchOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
            new(BlobOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion)
        ];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
            => ValueTask.FromResult<IPreparedVisionModel>(new Prepared());
        public async ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
        {
            if (operation is TemplateMatchOperation match)
            {
                return new(new TemplateMatchOutput(match.Image.FrameId, match.Image.CoordinateSpaceId,
                    "fake transaction", [new(new(4, 5), new(), 0, 1, 1)]));
            }
            var blob = (BlobOperation)operation;
            Entered.TrySetResult();
            await Resume.Task;
            return new(new BlobOutput(blob.Image.FrameId, blob.Image.CoordinateSpaceId,
                [new BlobItem(1, 1, new(16, 6), new(16, 6, 1, 1), [new RegionContour([new(16, 6)])], new(0, 0, null))]));
        }
        public void Dispose()
        {
        }
        private sealed class Prepared : IPreparedVisionModel
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class NoExecutionBackend : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => throw new InvalidOperationException("Must not validate numeric operations through SDK");
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token) => throw new InvalidOperationException("Must not execute numeric operations through SDK");
        public void Dispose()
        {
        }
    }

    private static (VisionFrame Template, VisionFrame Scene) Scene()
    {
        var template = EngineSyntheticFrames.Pattern(8, 8);
        var pixels = new byte[40 * 24];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                pixels[(y + 5) * 40 + x + 4] = template.Pixels[y * 8 + x];
            }
        }
        Rect(pixels, 40, 15, 6, 2, 2);
        Rect(pixels, 40, 20, 7, 3, 2);
        Rect(pixels, 40, 32, 16, 2, 2);
        return (template, Gray(40, 24, pixels));
    }

    private static VisionFrame Gray(int width, int height, byte[] pixels)
        => new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);

    private static void Rect(byte[] pixels, int stride, int x, int y, int width, int height)
    {
        for (var row = y; row < y + height; row++)
        {
            pixels.AsSpan(row * stride + x, width).Fill(255);
        }
    }
}
