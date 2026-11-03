using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class SegmentationToolsTests
{
    [Fact]
    public async Task LinearTransform_SaturatesInvertsAndExplicitlyRoundsHalfUp_WithoutReadingPadding()
    {
        byte[] pixels = [0, 5, 10, 77, 128, 200, 255, 77];
        var input = Frame(3, 2, PixelFormat.Mono8, pixels, 4);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var stats = await engine.ExecuteAsync(context, new GrayStatisticsOperation(input), default);
        Assert.Equal(VisionExecutionStatus.Success, stats.Status);
        Assert.Equal(0, stats.Value!.Minimum);
        Assert.Equal(255, stats.Value.Maximum);
        var linear = await engine.ExecuteAsync(context, new GrayLinearOperation(input, 2, -10, PixelFormat.Mono8, GrayLinearRounding.HalfUp), default);
        Assert.Equal(VisionExecutionStatus.Success, linear.Status);
        Assert.Equal(new byte[] { 0, 0, 10, 246, 255, 255 }, linear.Value!.Image.Pixels.ToArray());
        var inverse = await engine.ExecuteAsync(context, new GrayLinearOperation(input, -1, 255, PixelFormat.Mono8, GrayLinearRounding.HalfUp), default);
        Assert.Equal(new byte[] { 255, 250, 245, 127, 55, 0 }, inverse.Value!.Image.Pixels.ToArray());
        var half = await engine.ExecuteAsync(context, new GrayLinearOperation(input, 0.5, 0, PixelFormat.Mono8, GrayLinearRounding.HalfUp), default);
        Assert.Equal(3, half.Value!.Image.Pixels[1]);
        Assert.Equal(input.FrameId, half.Value.FrameId);
        Assert.NotEqual(input.FrameId, half.Value.Image.FrameId);
        Assert.Equal(pixels, input.Pixels.ToArray());
    }

    [Theory]
    [InlineData(2, 16, 64)]
    [InlineData(0.5, 128, 181)]
    public async Task Gamma_UsesExplicitExponentAndStableEndpoints(double gamma, byte at64, byte at128)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayGammaOperation(Frame(4, 1, PixelFormat.Mono8, [0, 64, 128, 255]), gamma), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 0, at64, at128, 255 }, result.Value!.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task Normalize_UsesPerFrameExtrema_AndConstantImageIsUnchanged()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayNormalizeOperation(Frame(3, 1, PixelFormat.Mono8, [20, 30, 40])), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 0, 128, 255 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(20, result.Value.InputMinimum);
        Assert.Equal(40, result.Value.InputMaximum);
        var constant = await engine.ExecuteAsync(context, new GrayNormalizeOperation(Frame(2, 1, PixelFormat.Mono8, [123, 123])), default);
        Assert.Equal(VisionExecutionStatus.Success, constant.Status);
        Assert.Equal(new byte[] { 123, 123 }, constant.Value!.Image.Pixels.ToArray());
    }

    [Theory]
    [InlineData(AdaptiveThresholdMethod.Mean)]
    [InlineData(AdaptiveThresholdMethod.Gaussian)]
    public async Task AdaptiveThreshold_UsesLocalNeighborhoodAndInverseComplement(AdaptiveThresholdMethod method)
    {
        var input = Frame(3, 3, PixelFormat.Mono8, [10, 10, 10, 10, 100, 10, 10, 10, 10]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new AdaptiveThresholdOperation(input, method, 3, 0), default);
        var inverse = await engine.ExecuteAsync(context, new AdaptiveThresholdOperation(input, method, 3, 0, true), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(VisionExecutionStatus.Success, inverse.Status);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 255, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(result.Value.Image.Pixels.ToArray().Select(value => (byte)(255 - value)), inverse.Value!.Image.Pixels.ToArray());
        Assert.Equal(1, result.Value.Image.Pixels.ToArray().Count(value => value == 255));
        Assert.Equal(100, input.Pixels[4]);
    }

    [Theory]
    [InlineData(PixelFormat.Bgr24)]
    [InlineData(PixelFormat.Bgra32)]
    [InlineData(PixelFormat.Rgb24)]
    public async Task HsvRedWrap_RespectsStorageOrderPaddingAndExplicitAlphaDiscard(PixelFormat format)
    {
        byte[][] bgr = [[0, 0, 255], [10, 0, 255], [0, 10, 255], [255, 0, 0], [0, 255, 0], [255, 255, 255], [128, 128, 130], [0, 0, 10]];
        var bpp = format == PixelFormat.Bgra32 ? 4 : 3;
        var stride = 2 * bpp + 2;
        var pixels = Enumerable.Repeat((byte)88, stride * 4).ToArray();
        for (var i = 0; i < bgr.Length; i++)
        {
            var offset = i / 2 * stride + i % 2 * bpp;
            pixels[offset] = bgr[i][format == PixelFormat.Rgb24 ? 2 : 0];
            pixels[offset + 1] = bgr[i][1];
            pixels[offset + 2] = bgr[i][format == PixelFormat.Rgb24 ? 0 : 2];
            if (bpp == 4)
            {
                pixels[offset + 3] = i % 2 == 0 ? (byte)0 : (byte)255;
            }
        }
        var input = Frame(2, 4, format, pixels, stride);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var selected = input;
        if (format == PixelFormat.Bgra32)
        {
            var converted = await engine.ExecuteAsync(context, new ImageConvertOperation(input, PixelFormat.Bgr24), default);
            Assert.Equal(VisionExecutionStatus.Success, converted.Status);
            selected = converted.Value!.Image;
        }
        var result = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(selected, new(170, 10, 64, 255, 32, 255)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 255, 255, 255, 0, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(3, result.Value.Image.Pixels.ToArray().Count(value => value == 255));
        Assert.Equal(selected.FrameId, result.Value.FrameId);
        Assert.Equal(pixels, input.Pixels.ToArray());
        var exact = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(selected, new(0, 0, 64, 255, 32, 255)), default);
        Assert.Equal(1, exact.Value!.Image.Pixels.ToArray().Count(value => value == 255));
    }

    [Fact]
    public async Task RgbRange_IsInclusiveAndRejectsHueStyleWrap()
    {
        var input = Frame(3, 1, PixelFormat.Rgb24, [100, 50, 10, 99, 50, 10, 101, 50, 10]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new RgbColorSegmentOperation(input, new(100, 100, 50, 50, 10, 10)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 255, 0, 0 }, result.Value!.Image.Pixels.ToArray());
        Assert.Throws<ArgumentException>(() => new RgbColorRange(200, 10, 0, 255, 0, 255));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HsvColorRange(200, 10, 0, 255, 0, 255));
        Assert.Throws<ArgumentException>(() => new HsvColorRange(170, 10, 80, 50, 0, 255));
    }

    [Fact]
    public async Task RequestsFreezeFramesAndParameters_AfterInputsChange()
    {
        byte[] pixels = [10, 10, 10, 10, 100, 10, 10, 10, 10];
        var input = Frame(3, 3, PixelFormat.Mono8, pixels);
        var transform = new GrayLinearOperation(input, 2, 0, PixelFormat.Mono8, GrayLinearRounding.HalfUp);
        var adaptive = new AdaptiveThresholdOperation(input, AdaptiveThresholdMethod.Mean, 3, 0);
        Array.Clear(pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var transformed = await engine.ExecuteAsync(context, transform, default);
        var binary = await engine.ExecuteAsync(context, adaptive, default);
        Assert.Equal(VisionExecutionStatus.Success, transformed.Status);
        Assert.Equal(VisionExecutionStatus.Success, binary.Status);
        Assert.Equal(200, transformed.Value!.Image.Pixels[4]);
        Assert.Equal(1, binary.Value!.Image.Pixels.ToArray().Count(value => value == 255));
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(200, transformed.Value.Image.Pixels[4]);
    }

    private static VisionFrame Frame(int width, int height, PixelFormat format, byte[] pixels, int stride = 0)
        => new(Guid.NewGuid(), Guid.NewGuid(), width, height, format, pixels, stride);
}
