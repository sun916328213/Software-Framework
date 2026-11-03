using System.Buffers.Binary;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class ImagePreprocessingTests
{
    [Theory]
    [InlineData(ImageFilterKind.Mean)]
    [InlineData(ImageFilterKind.Gaussian)]
    public async Task Smoothing_PreservesConstantAndSource_AndMeanHonoursExplicitZeroBorder(ImageFilterKind kind)
    {
        var frame = Gray(3, 3, Enumerable.Repeat((byte)90, 9).ToArray());
        var before = frame.Pixels.ToArray();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var operation = new ImageFilterOperation(frame, kind, 3, ImageBorderMode.Replicate,
            kind == ImageFilterKind.Gaussian ? 1 : null);
        var result = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(before, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(before, frame.Pixels.ToArray());
        Assert.NotEqual(frame.FrameId, result.Value.Image.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, result.Value.Image.CoordinateSpaceId);
        var impulse = new byte[25];
        impulse[12] = 255;
        var blurred = await engine.ExecuteAsync(context,
            new ImageFilterOperation(Gray(5, 5, impulse), kind, 3, ImageBorderMode.Replicate,
                kind == ImageFilterKind.Gaussian ? 1 : null), default);
        Assert.Equal(VisionExecutionStatus.Success, blurred.Status);
        Assert.InRange(blurred.Value!.Image.Pixels[12], 1, 254);
        Assert.True(blurred.Value.Image.Pixels[11] > 0);
        Assert.Equal(0, blurred.Value.Image.Pixels[0]);
        if (kind == ImageFilterKind.Mean)
        {
            var zeroBorder = await engine.ExecuteAsync(context,
                new ImageFilterOperation(frame, kind, 3, ImageBorderMode.ConstantZero), default);
            Assert.Equal(VisionExecutionStatus.Success, zeroBorder.Status);
            Assert.Equal(40, zeroBorder.Value!.Image.Pixels[0]); // 角点核中4个90，另外5个补零。
            Assert.Equal(90, zeroBorder.Value.Image.Pixels[4]);
        }
    }

    [Fact]
    public async Task Median_Preserves16BitAndIgnoresPadding_WhileRemovingSingleImpulse()
    {
        var bytes = Enumerable.Repeat((byte)73, 12 * 5).ToArray();
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(y * 12 + x * 2), (ushort)(x == 2 && y == 2 ? 50000 : 1000));
            }
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 5, 5, PixelFormat.Mono16, bytes, 12);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context,
            new ImageFilterOperation(frame, ImageFilterKind.Median, 3, ImageBorderMode.Replicate), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(PixelFormat.Mono16, result.Value!.Image.Format);
        for (var i = 0; i < 25; i++)
        {
            Assert.Equal(1000, BinaryPrimitives.ReadUInt16LittleEndian(result.Value.Image.Pixels.Slice(i * 2, 2)));
        }
        Assert.Equal(bytes, frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Otsu_UsesOriginalDepth_ReportsThreshold_AndInverseComplementsResult(bool mono16)
    {
        var low = (ushort)(mono16 ? 1000 : 20);
        var high = (ushort)(mono16 ? 5000 : 200);
        ushort[] values = [low, low, high, high, low, low, high, high];
        var pixels = new byte[mono16 ? 16 : 8];
        for (var i = 0; i < values.Length; i++)
        {
            if (mono16)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(i * 2), values[i]);
            }
            else
            {
                pixels[i] = (byte)values[i];
            }
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 4, 2, mono16 ? PixelFormat.Mono16 : PixelFormat.Mono8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new OtsuThresholdOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(low, result.Value!.Threshold);
        Assert.Equal(PixelFormat.Mono8, result.Value.Image.Format);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0, 255, 255 }, result.Value.Image.Pixels.ToArray());
        var inverse = await engine.ExecuteAsync(context, new OtsuThresholdOperation(frame, true), default);
        Assert.Equal(VisionExecutionStatus.Success, inverse.Status);
        Assert.Equal(result.Value.Threshold, inverse.Value!.Threshold);
        for (var i = 0; i < 8; i++)
        {
            Assert.Equal(255, result.Value.Image.Pixels[i] + inverse.Value.Image.Pixels[i]);
        }
        var black = await engine.ExecuteAsync(context, new OtsuThresholdOperation(Gray(1, 1, [0])), default);
        Assert.Equal(VisionExecutionStatus.Success, black.Status);
        Assert.Equal(0, black.Value!.Image.Pixels[0]);
    }

    [Theory]
    [InlineData(AdaptiveThresholdMethod.Mean)]
    [InlineData(AdaptiveThresholdMethod.Gaussian)]
    public async Task AdaptiveThreshold_UsesLocalWindowOffsetAndInverse(AdaptiveThresholdMethod method)
    {
        var frame = Gray(3, 3, [0, 0, 0, 0, 90, 0, 0, 0, 0]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new AdaptiveThresholdOperation(frame, method, 3, 0), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 255, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
        var inverse = await engine.ExecuteAsync(context, new AdaptiveThresholdOperation(frame, method, 3, 0, true), default);
        Assert.Equal(VisionExecutionStatus.Success, inverse.Status);
        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(255, result.Value.Image.Pixels[i] + inverse.Value!.Image.Pixels[i]);
        }
        // 右边界20：复制边界均值round(140/9)=16，高斯加权均值=20，严格大于判定应不同。
        var asymmetric = Gray(3, 3, [0, 0, 0, 0, 100, 20, 0, 0, 0]);
        var weighted = await engine.ExecuteAsync(context, new AdaptiveThresholdOperation(asymmetric, method, 3, 0), default);
        Assert.Equal(VisionExecutionStatus.Success, weighted.Status);
        Assert.Equal(method == AdaptiveThresholdMethod.Mean ? 255 : 0, weighted.Value!.Image.Pixels[5]);
        var flat = Gray(3, 3, Enumerable.Repeat((byte)100, 9).ToArray());
        var shifted = await engine.ExecuteAsync(context, new AdaptiveThresholdOperation(flat, method, 3, 5), default);
        Assert.Equal(VisionExecutionStatus.Success, shifted.Status);
        Assert.All(shifted.Value!.Image.Pixels.ToArray(), value => Assert.Equal(255, value));
    }

    [Theory]
    [InlineData(AdaptiveThresholdMethod.Mean, -1, 0)]
    [InlineData(AdaptiveThresholdMethod.Mean, 0, 0)]
    [InlineData(AdaptiveThresholdMethod.Mean, 1, 255)]
    [InlineData(AdaptiveThresholdMethod.Gaussian, -1, 0)]
    [InlineData(AdaptiveThresholdMethod.Gaussian, 0, 0)]
    [InlineData(AdaptiveThresholdMethod.Gaussian, 1, 255)]
    public async Task AdaptiveThreshold_SinglePixelUsesReplicatedBoundaryAndIgnoresPadding(AdaptiveThresholdMethod method, int offset, byte expected)
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Mono8, [100, 255, 255], 3);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new AdaptiveThresholdOperation(frame, method, 3, offset), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, Assert.Single(result.Value!.Image.Pixels.ToArray()));
        Assert.Equal(new byte[] { 100, 255, 255 }, frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData(MorphologyKernelShape.Rectangle, 25)]
    [InlineData(MorphologyKernelShape.Cross, 9)]
    [InlineData(MorphologyKernelShape.Ellipse, 17)]
    public async Task Morphology_HonoursShape_AndDoesNotChangeSource(MorphologyKernelShape shape, int expected)
    {
        var pixels = new byte[49];
        pixels[24] = 255;
        var frame = Gray(7, 7, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var dilated = await engine.ExecuteAsync(context,
            new MorphologyOperation(frame, MorphologyKind.Dilate, shape, 5, 5, ImageBorderMode.ConstantZero), default);
        Assert.Equal(VisionExecutionStatus.Success, dilated.Status);
        Assert.Equal(expected, CountWhite(dilated.Value!.Image));
        var eroded = await engine.ExecuteAsync(context,
            new MorphologyOperation(dilated.Value.Image, MorphologyKind.Erode, shape, 5, 5, ImageBorderMode.ConstantZero), default);
        Assert.Equal(VisionExecutionStatus.Success, eroded.Status);
        Assert.Equal(pixels, eroded.Value!.Image.Pixels.ToArray());
        Assert.Equal(pixels, frame.Pixels.ToArray());
    }

    [Fact]
    public async Task OpeningClosingAndIterations_HaveRealBinaryEffects_AndZeroBorderIsNotSdkMagicDefault()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var pixels = new byte[25];
        pixels[12] = 255;
        var opened = await engine.ExecuteAsync(context,
            new MorphologyOperation(Gray(5, 5, pixels), MorphologyKind.Open, MorphologyKernelShape.Rectangle, 3, 3, ImageBorderMode.ConstantZero), default);
        Assert.Equal(VisionExecutionStatus.Success, opened.Status);
        Assert.Equal(0, CountWhite(opened.Value!.Image));
        var repeated = await engine.ExecuteAsync(context,
            new MorphologyOperation(Gray(5, 5, pixels), MorphologyKind.Dilate, MorphologyKernelShape.Rectangle, 3, 3, ImageBorderMode.ConstantZero, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, repeated.Status);
        Assert.Equal(25, CountWhite(repeated.Value!.Image));
        var hole = new byte[25];
        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
            {
                hole[y * 5 + x] = 255;
            }
        }
        hole[12] = 0;
        var closed = await engine.ExecuteAsync(context,
            new MorphologyOperation(Gray(5, 5, hole), MorphologyKind.Close, MorphologyKernelShape.Rectangle, 3, 3, ImageBorderMode.ConstantZero), default);
        Assert.Equal(VisionExecutionStatus.Success, closed.Status);
        Assert.Equal(9, CountWhite(closed.Value!.Image));
        var full = Gray(5, 5, Enumerable.Repeat((byte)255, 25).ToArray());
        var edge = await engine.ExecuteAsync(context,
            new MorphologyOperation(full, MorphologyKind.Erode, MorphologyKernelShape.Rectangle, 3, 3, ImageBorderMode.ConstantZero), default);
        Assert.Equal(VisionExecutionStatus.Success, edge.Status);
        Assert.Equal(9, CountWhite(edge.Value!.Image));
    }

    [Fact]
    public async Task UnsupportedCombinationsAndInvalidParameters_AreNotIgnoredOrConverted()
    {
        var mono16 = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Mono16, [1, 0]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var adaptive = await engine.ExecuteAsync(context, new AdaptiveThresholdOperation(mono16, AdaptiveThresholdMethod.Mean, 3, 0), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, adaptive.Status);
        Assert.Null(adaptive.Value);
        var median = await engine.ExecuteAsync(context, new ImageFilterOperation(mono16, ImageFilterKind.Median, 7, ImageBorderMode.Replicate), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, median.Status);
        var gray = Gray(1, 1, [20]);
        var border = await engine.ExecuteAsync(context,
            new ImageFilterOperation(gray, ImageFilterKind.Median, 3, ImageBorderMode.Reflect101), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, border.Status);
        Assert.Null(border.Value);
        Assert.Throws<ArgumentException>(() => new MorphologyOperation(gray, MorphologyKind.Dilate, MorphologyKernelShape.Rectangle, 3, 3, ImageBorderMode.ConstantZero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageFilterOperation(gray, ImageFilterKind.Mean, 4, ImageBorderMode.Replicate));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageFilterOperation(gray, ImageFilterKind.Gaussian, 3, ImageBorderMode.Replicate));
        Assert.Throws<ArgumentException>(() => new ImageFilterOperation(gray, ImageFilterKind.Mean, 3, ImageBorderMode.Replicate, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdaptiveThresholdOperation(gray, AdaptiveThresholdMethod.Mean, 4, 0));
    }

    private static VisionFrame Gray(int width, int height, byte[] pixels)
        => new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);

    private static int CountWhite(VisionFrame frame) => frame.Pixels.ToArray().Count(value => value == 255);
}
