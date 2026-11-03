using System.Buffers.Binary;
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
public sealed class AlignedImageDifferenceEngineTests
{
    [Theory]
    [InlineData(false, ImageDifferenceDirection.Absolute)]
    [InlineData(false, ImageDifferenceDirection.CurrentBrighter)]
    [InlineData(false, ImageDifferenceDirection.CurrentDarker)]
    [InlineData(true, ImageDifferenceDirection.Absolute)]
    [InlineData(true, ImageDifferenceDirection.CurrentBrighter)]
    [InlineData(true, ImageDifferenceDirection.CurrentDarker)]
    public async Task ExplicitDirections_UseOriginalBitDepthAndInclusiveDifference_WithoutUnsignedWrap(bool mono16, ImageDifferenceDirection direction)
    {
        var space = Guid.NewGuid();
        var level = mono16 ? 10000 : 100;
        var delta = mono16 ? 3000 : 20;
        var maximum = mono16 ? ushort.MaxValue : byte.MaxValue;
        ushort[] currentValues = [(ushort)(level - delta), (ushort)(level + delta), (ushort)level, 0, (ushort)maximum,
            (ushort)level, (ushort)(level - delta + 1), (ushort)(level + delta - 1)];
        ushort[] referenceValues = [(ushort)level, (ushort)level, (ushort)level, (ushort)maximum, 0,
            (ushort)level, (ushort)level, (ushort)level];
        var current = Gray(4, 2, mono16, space, currentValues, 3);
        var reference = Gray(4, 2, mono16, space, referenceValues, 5);
        var beforeCurrent = current.Pixels.ToArray();
        var beforeReference = reference.Pixels.ToArray();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new AlignedImageDifferenceOperation(current, reference, direction, (ushort)delta, (ushort)maximum);
        Assert.Same(current, request.Current);
        Assert.Same(reference, request.Reference);
        Assert.Null(request.Model);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        byte[] expected = direction switch
        {
            ImageDifferenceDirection.Absolute => [255, 255, 0, 255, 255, 0, 0, 0],
            ImageDifferenceDirection.CurrentBrighter => [0, 255, 0, 0, 255, 0, 0, 0],
            _ => [255, 0, 0, 255, 0, 0, 0, 0]
        };
        Assert.Equal(expected, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(current.FrameId, result.Value.FrameId);
        Assert.Equal(reference.FrameId, result.Value.ReferenceFrameId);
        Assert.Equal(direction, result.Value.Direction);
        Assert.Equal((ushort)delta, result.Value.MinimumDifference);
        Assert.Equal((ushort)maximum, result.Value.MaximumDifference);
        Assert.NotEqual(current.FrameId, result.Value.Image.FrameId);
        Assert.NotEqual(reference.FrameId, result.Value.Image.FrameId);
        Assert.Equal(space, result.Value.Image.CoordinateSpaceId);
        Assert.Equal(PixelFormat.Mono8, result.Value.Image.Format);
        Assert.Equal(current.Width, result.Value.Image.Width);
        Assert.Equal(current.Height, result.Value.Image.Height);
        Assert.Equal(beforeCurrent, current.Pixels.ToArray());
        Assert.Equal(beforeReference, reference.Pixels.ToArray());
    }

    [Fact]
    public async Task FrozenRegionAndUpperThreshold_ExcludeOutsideAndExcessiveDifferences()
    {
        var space = Guid.NewGuid();
        var reference = Gray(4, 3, false, space, Enumerable.Repeat((ushort)100, 12).ToArray());
        ushort[] values = [150, 120, 140, 120, 120, 119, 141, 120, 120, 120, 120, 120];
        var current = Gray(4, 3, false, space, values);
        var roi = new Roi { Kind = RoiKind.Rectangle, Pose = new Pose2D(2, 1, 0), Size = new Size2D(2, 2) };
        var request = new AlignedImageDifferenceOperation(current, reference, ImageDifferenceDirection.Absolute, 20, 40, new SearchRegion(roi));
        roi.Pose = new(100, 100, 0);
        Assert.Equal(VisionOperationFeatures.ProcessingRegion, request.RequiredFeatures);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 0, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task IdenticalExplicitReference_IsBlackSuccess_NotNoMatch_AndNoDefectCountPasses()
    {
        var frame = Gray(4, 2, false, Guid.NewGuid(), Enumerable.Repeat((ushort)100, 8).ToArray());
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new AlignedImageDifferenceOperation(frame, frame,
            ImageDifferenceDirection.Absolute, 1, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.All(result.Value!.Image.Pixels.ToArray(), value => Assert.Equal(0, value));
        var blobs = await engine.ExecuteAsync(context, new BlobOperation(result.Value.Image, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, blobs.Status);
        var decision = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForCount(blobs.Value!, 0, 0), default);
        Assert.True(decision.Value!.Passed);
    }

    [Fact]
    public async Task ActualBrightAndDarkDefects_FeedBlobAndAreaCountDecisions_AfterSourcesRelease()
    {
        var space = Guid.NewGuid();
        var reference = Gray(12, 8, false, space, Enumerable.Repeat((ushort)100, 96).ToArray(), 2);
        var pixels = Enumerable.Repeat((ushort)100, 96).ToArray();
        for (var y = 2; y < 4; y++)
        {
            pixels[y * 12 + 1] = pixels[y * 12 + 2] = 150;
        }
        for (var y = 3; y < 6; y++)
        {
            pixels[y * 12 + 7] = pixels[y * 12 + 8] = 50;
        }
        pixels[0] = 105; // 明确低于配方差异限，不自动归一化或把噪声也算缺陷。
        var current = Gray(12, 8, false, space, pixels, 3);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var differencing = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(differencing, new AlignedImageDifferenceOperation(current, reference,
            ImageDifferenceDirection.Absolute, 20, 100), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var alternate = await engine.ExecuteAsync(differencing, new AlignedImageDifferenceOperation(current, current,
            ImageDifferenceDirection.Absolute, 20, 100), default);
        Assert.Equal(VisionExecutionStatus.Success, alternate.Status);
        Assert.All(alternate.Value!.Image.Pixels.ToArray(), value => Assert.Equal(0, value));
        await engine.ReleaseContextAsync(differencing, default);
        var inspecting = await engine.CreateContextAsync(default);
        var blobs = await engine.ExecuteAsync(inspecting, new BlobOperation(result.Value!.Image, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, blobs.Status);
        Assert.Equal(new long[] { 6, 4 }, blobs.Value!.Items.Select(item => item.Area));
        Assert.Equal(result.Value.Image.FrameId, blobs.Value.FrameId);
        Assert.Equal(space, blobs.Value.CoordinateSpaceId);
        await engine.ReleaseContextAsync(inspecting, default);
        var evaluating = await engine.CreateContextAsync(default);
        var count = await engine.ExecuteAsync(evaluating, BlobJudgmentOperation.ForCount(blobs.Value, 2, 2), default);
        var ng = await engine.ExecuteAsync(evaluating, BlobJudgmentOperation.ForCount(blobs.Value, 0, 0), default);
        var area = await engine.ExecuteAsync(evaluating, BlobJudgmentOperation.ForArea(blobs.Value, 1, 6, 6), default);
        Assert.True(count.Value!.Passed);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
        Assert.True(area.Value!.Passed);
    }

    [Fact]
    public void MisalignedDimensionsFormatsSpacesOrReusedFrameIds_AreRejectedBeforeSdk()
    {
        var space = Guid.NewGuid();
        var current = Gray(4, 2, false, space, new ushort[8]);
        Assert.Throws<ArgumentException>(() => new AlignedImageDifferenceOperation(current,
            Gray(2, 4, false, space, new ushort[8]), ImageDifferenceDirection.Absolute, 1, 255));
        Assert.Throws<ArgumentException>(() => new AlignedImageDifferenceOperation(current,
            Gray(4, 2, true, space, new ushort[8]), ImageDifferenceDirection.Absolute, 1, 255));
        Assert.Throws<ArgumentException>(() => new AlignedImageDifferenceOperation(current,
            Gray(4, 2, false, Guid.NewGuid(), new ushort[8]), ImageDifferenceDirection.Absolute, 1, 255));
        var impostor = new VisionFrame(current.FrameId, space, 4, 2, current.Format, current.Pixels);
        Assert.Throws<ArgumentException>(() => new AlignedImageDifferenceOperation(current, impostor, ImageDifferenceDirection.Absolute, 1, 255));
    }

    [Theory]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Float32, 4)]
    public async Task NonGrayPair_IsUnsupported_NotImplicitColorOrFloatNormalization(PixelFormat format, int bytes)
    {
        var space = Guid.NewGuid();
        var current = new VisionFrame(Guid.NewGuid(), space, 2, 2, format, new byte[4 * bytes]);
        var reference = new VisionFrame(Guid.NewGuid(), space, 2, 2, format, new byte[4 * bytes]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new AlignedImageDifferenceOperation(current, reference,
            ImageDifferenceDirection.Absolute, 1, 255), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Equal("UnsupportedPixelFormat", result.Issue!.Code);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task WrongReferenceResult_IsRejectedBeforePublication()
    {
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => new WrongReferenceBackend());
        var context = await engine.CreateContextAsync(default);
        var space = Guid.NewGuid();
        var current = Gray(4, 2, false, space, new ushort[8]);
        var reference = Gray(4, 2, false, space, new ushort[8]);
        var result = await engine.ExecuteAsync(context, new AlignedImageDifferenceOperation(current, reference,
            ImageDifferenceDirection.Absolute, 1, 255), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task InvalidDirectionsZeroRangeEmptyRegionAndWrongDepthThreshold_AreRejected()
    {
        var frame = Gray(4, 2, false, Guid.NewGuid(), new ushort[8]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlignedImageDifferenceOperation(frame, frame,
            (ImageDifferenceDirection)99, 1, 255));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlignedImageDifferenceOperation(frame, frame,
            ImageDifferenceDirection.Absolute, 0, 255));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlignedImageDifferenceOperation(frame, frame,
            ImageDifferenceDirection.Absolute, 20, 19));
        Assert.Throws<ArgumentException>(() => new AlignedImageDifferenceOperation(frame, frame,
            ImageDifferenceDirection.Absolute, 1, 255, new SearchRegion(Roi.Rectangle(100, 100, 2, 2))));
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var issues = await engine.ValidateAsync(context, new AlignedImageDifferenceOperation(frame, frame,
            ImageDifferenceDirection.Absolute, 1, 256), default);
        Assert.Contains(issues, issue => issue.Code == "DifferenceRangeMismatch");
    }

    private static VisionFrame Gray(int width, int height, bool mono16, Guid space, ushort[] values, int padding = 0)
    {
        var bytes = mono16 ? 2 : 1;
        var stride = width * bytes + padding;
        var pixels = Enumerable.Repeat((byte)221, stride * height).ToArray();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = values[y * width + x];
                if (mono16)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(y * stride + x * bytes, 2), value);
                }
                else
                {
                    pixels[y * stride + x] = (byte)value;
                }
            }
        }
        return new(Guid.NewGuid(), space, width, height, mono16 ? PixelFormat.Mono16 : PixelFormat.Mono8, pixels, stride);
    }

    private sealed class WrongReferenceBackend : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [new(AlignedImageDifferenceOperation.Id, Domain, VisionOperationFeatures.None)];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
        {
            var request = (AlignedImageDifferenceOperation)operation;
            var image = new VisionFrame(Guid.NewGuid(), request.Image.CoordinateSpaceId, request.Image.Width, request.Image.Height,
                PixelFormat.Mono8, new byte[request.Image.Width * request.Image.Height]);
            return ValueTask.FromResult(new BackendOutput(new ImageDifferenceOutput(request.Image.FrameId, Guid.NewGuid(),
                request.Direction, request.MinimumDifference, request.MaximumDifference, image)));
        }
        public void Dispose()
        {
        }
    }
}
