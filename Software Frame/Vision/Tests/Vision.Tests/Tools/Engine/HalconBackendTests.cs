using Vision.Core;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Vision.Tools.Execution;
using Vision.Tools.Halcon.Engine;
using Xunit;

namespace Vision.Tests.Tools.Engine;

// 使用SDK边界fake，不激活HALCON Runtime；坐标golden来自23.11官方affine_trans_pixel示例。
[Trait("Category", "Contract")]
public sealed class HalconBackendTests
{
    [Fact]
    public async Task SelectedHalcon_PreservesMaskAndAnchor_AndConvertsNativePoseToPixelCenters()
    {
        var native = new ProbeRuntime();
        await using var engine = Create(native);
        var context = await engine.CreateContextAsync(default);
        var mask = new VisionMask(4, 4, [255, 255, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0]);
        var definition = new TemplateModelDefinition(Frame(), new(3, 1), new(0, 360, 0.5, 2), mask, TemplateMatchMetric.ShapeEdges, true);
        var model = await engine.PrepareModelAsync(context, definition, 1, default);
        native.Model.Observations = [new(20, 30, -Math.PI / 2, 2, 0.95)];
        var image = Frame();
        var result = await engine.ExecuteAsync(context, new TemplateMatchOperation(image, model, new(0, 360, 0.5, 2),
            sampling: TemplateSearchSampling.NativeAutomatic), default);
        Assert.Same(definition, native.Definition);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(image.FrameId, result.Value!.FrameId);
        var found = Assert.Single(result.Value.Matches);
        Assert.Equal(new Point2D(28.5, 20.5), found.Position);
        Assert.Equal(new Point2D(3, 1), found.ModelOrigin);
        Assert.Equal(90, found.AngleDegrees, 8); Assert.Equal(2, found.Scale);
        native.Model.Observations = [];
        var empty = await engine.ExecuteAsync(context, new TemplateMatchOperation(image, model, definition.PreparedRange,
            sampling: TemplateSearchSampling.NativeAutomatic), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, empty.Status); Assert.Empty(empty.Value!.Matches);
        await engine.ReleaseContextAsync(context, default);
        Assert.True(native.Model.Disposed);
    }

    [Fact]
    public async Task UnsupportedSamplingSearchDomainAndMetric_AreRejectedBeforeSdkWork()
    {
        var native = new ProbeRuntime();
        await using var engine = Create(native);
        var context = await engine.CreateContextAsync(default);
        var definition = Definition();
        var model = await engine.PrepareModelAsync(context, definition, 1, default);
        var discrete = new TemplateMatchOperation(Frame(), model, new());
        Assert.Contains(await engine.ValidateAsync(context, discrete, default), item => item.Code == "UnsupportedSampling");
        Assert.Equal(VisionExecutionStatus.Unsupported, (await engine.ExecuteAsync(context, discrete, default)).Status);
        var area = new VisionMask(4, 4, Enumerable.Repeat((byte)255, 16).ToArray());
        var masked = new TemplateMatchOperation(Frame(), model, new(), searchMask: area, sampling: TemplateSearchSampling.NativeAutomatic);
        Assert.Equal(VisionExecutionStatus.Unsupported, (await engine.ExecuteAsync(context, masked, default)).Status);
        await Assert.ThrowsAsync<NotSupportedException>(() => engine.PrepareModelAsync(context,
            new TemplateModelDefinition(Frame(), new(), new()), 2, default).AsTask());
        Assert.Equal(1, native.Prepares); Assert.Equal(0, native.Model.Runs);
        Assert.False(native.Model.Disposed);
    }

    [Fact]
    public async Task NativeRefinementOutsideRequestedRange_FailsWithoutPublishingPartialResults()
    {
        var native = new ProbeRuntime();
        await using var engine = Create(native);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, Definition(), 1, default);
        native.Model.Observations = [new(20, 30, Math.PI / 6, 1, 0.95)];
        var result = await engine.ExecuteAsync(context, new TemplateMatchOperation(Frame(), model, new(),
            sampling: TemplateSearchSampling.NativeAutomatic), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status); Assert.Null(result.Value);
        Assert.Equal(1, native.Model.Runs);
    }

    [Fact]
    public void CircularRangeAndPixelConvention_DoNotSilentlyClipOrSwapRawCoordinates()
    {
        Assert.True(HalconShapeCoordinates.TryNativeRange(new(170, 20), new(175, 5), out var native));
        Assert.Equal(Math.PI, native.Start, 12); Assert.Equal(5 * Math.PI / 180, native.Extent, 12);
        Assert.False(HalconShapeCoordinates.TryNativeRange(new(0, 360), new(-5, 10), out _));
        Assert.Equal(new Point2D(30, 20), HalconShapeCoordinates.ToCandidate(new(20, 30, 0, 1, 0.9), new()).Position);
        Assert.Equal(new Point2D(30.5, 18.5), HalconShapeCoordinates.ToCandidate(new(20, 30, Math.PI / 2, 2, 0.9), new()).Position);
        Assert.True(TemplateSearchSampling.NativeAutomatic.UsesNativeSearch);
        Assert.False(new TemplateSearchSampling(2, 0.05).UsesNativeSearch);
    }

    private static TemplateModelDefinition Definition() => new(Frame(), new(3, 1), new(0, 360, 0.5, 2), metric: TemplateMatchMetric.ShapeEdges);
    private static VisionFrame Frame() => new(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Mono8, Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());
    private static IVisionEngine Create(ProbeRuntime runtime) => VisionEngineFactory.CreateConfigured(new("halcon"), (domain, id, device) =>
    {
        Assert.Equal(Vision.Core.Capabilities.VisionExecutionDomain.Vision, domain);
        Assert.Equal("halcon", id); Assert.Null(device);
        return new HalconBackend(runtime);
    });

    private sealed class ProbeRuntime : IHalconVisionRuntime
    {
        public Exception? CleanupFailure => null;
        public ImageCropOutput Crop(ImageCropOperation operation) => throw new NotSupportedException();
        public GrayStatisticsOutput Statistics(GrayStatisticsOperation operation) => throw new NotSupportedException();
        internal readonly ProbeModel Model = new();
        internal TemplateModelDefinition? Definition;
        internal int Prepares;
        public IHalconShapeModel Prepare(TemplateModelDefinition definition) { Definition = definition; Prepares++; return Model; }
        public BinaryImageOutput Threshold(ThresholdRangeOperation operation) => throw new NotSupportedException();
        public void Dispose() => Assert.True(Model.Disposed);
    }
    private sealed class ProbeModel : IHalconShapeModel
    {
        internal IReadOnlyList<HalconShapeObservation> Observations = [];
        internal int Runs;
        internal bool Disposed;
        public IReadOnlyList<HalconShapeObservation> Run(TemplateMatchOperation operation)
        { Assert.False(Disposed); Runs++; return Observations; }
        public void Dispose() => Disposed = true;
    }
}
