using System.Buffers.Binary;
using Vision.Core;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

/// <summary>原基础图像有效需求迁至不可变请求及唯一引擎；不恢复旧参数DTO、结果或PrepareRun会话。</summary>
[Trait("Category", "Unit")]
public sealed class BasicImageToolsTests : IAsyncLifetime
{
    private readonly IVisionEngine _engine = VisionEngineFactory.Create(new("opencv"));
    private VisionContext _context = null!;
    public async Task InitializeAsync() => _context = await _engine.CreateContextAsync(default);
    public async Task DisposeAsync() => await _engine.DisposeAsync();

    [Theory]
    [InlineData(PixelFormat.Rgb24)]
    [InlineData(PixelFormat.Bgr24)]
    [InlineData(PixelFormat.Bgra32)]
    public async Task ConvertColor_UsesDeclaredOrder_IgnoresAlphaAndPadding_AndPreservesSpace(PixelFormat format)
    {
        var rgb = format == PixelFormat.Rgb24;
        byte[] red = rgb ? [255, 0, 0] : [0, 0, 255];
        byte[] blue = rgb ? [0, 0, 255] : [255, 0, 0];
        if (format == PixelFormat.Bgra32)
        {
            red = [0, 0, 255, 0];
            blue = [255, 0, 0, 255];
        }

        var pixels = red.Concat(new byte[] { 99, 99 }).Concat(blue).Concat(new byte[] { 99, 99 }).ToArray();
        var image = Frame(1, 2, format, pixels, red.Length + 2);
        var original = image.Pixels.ToArray();
        var result = await Run(new ImageConvertOperation(image, PixelFormat.Mono8));
        Assert.Equal(new byte[] { 76, 29 }, result.Image.Pixels.ToArray());
        AssertTransformSource(image, result);
        Assert.Equal(1, result.Image.Stride);
        var detached = result.Image.Pixels.ToArray();
        detached[0] = 17;
        Assert.Equal(original, image.Pixels.ToArray());
        Assert.Equal(new byte[] { 76, 29 }, result.Image.Pixels.ToArray());
    }

    [Theory]
    [InlineData(PixelFormat.Rgb24, RgbChannel.Red, 100, 7)]
    [InlineData(PixelFormat.Rgb24, RgbChannel.Green, 50, 8)]
    [InlineData(PixelFormat.Rgb24, RgbChannel.Blue, 10, 9)]
    [InlineData(PixelFormat.Bgr24, RgbChannel.Red, 100, 7)]
    [InlineData(PixelFormat.Bgr24, RgbChannel.Green, 50, 8)]
    [InlineData(PixelFormat.Bgr24, RgbChannel.Blue, 10, 9)]
    [InlineData(PixelFormat.Bgra32, RgbChannel.Red, 100, 7)]
    [InlineData(PixelFormat.Bgra32, RgbChannel.Green, 50, 8)]
    [InlineData(PixelFormat.Bgra32, RgbChannel.Blue, 10, 9)]
    public async Task ConvertChannel_ExtractsSemanticChannel_WithoutLuminanceOrAlphaComposition(
        PixelFormat format, RgbChannel channel, byte first, byte second)
    {
        byte[] top = format == PixelFormat.Rgb24 ? [100, 50, 10] : [10, 50, 100];
        byte[] bottom = format == PixelFormat.Rgb24 ? [7, 8, 9] : [9, 8, 7];
        if (format == PixelFormat.Bgra32)
        {
            top = [10, 50, 100, 0];
            bottom = [9, 8, 7, 255];
        }

        var pixels = top.Concat(new byte[] { 199, 199 }).Concat(bottom).Concat(new byte[] { 199, 199 }).ToArray();
        var frame = Frame(1, 2, format, pixels, top.Length + 2);
        var request = new ColorChannelExtractOperation(frame, channel);
        Assert.Null(request.Model);
        var result = await Run(request);
        Assert.Equal(new byte[] { first, second }, result.Image.Pixels.ToArray());
        Assert.Equal(PixelFormat.Mono8, result.Image.Format);
        AssertTransformSource(frame, result);
        Assert.Equal(pixels, frame.Pixels.ToArray());
        await _engine.ReleaseContextAsync(_context, default);
        Assert.Equal(new byte[] { first, second }, result.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task ConvertMono16_UsesExplicitGainOffsetClippingAndSdkRounding_NotFrameNormalization()
    {
        var image = Frame(6, 1, PixelFormat.Mono16, [0, 0, 100, 0, 150, 0, 200, 0, 250, 0, 255, 255]);
        // 原[100,200]范围配方显式展开为255/(200-100)与-100×gain，不猜测本图极值。
        var gain = 255d / (200 - 100);
        var result = await Run(new GrayLinearOperation(image, gain, -100 * gain, PixelFormat.Mono8));
        Assert.Equal(new byte[] { 0, 0, 128, 255, 255, 255 }, result.Image.Pixels.ToArray());
        var other = await Run(new GrayLinearOperation(Frame(1, 1, PixelFormat.Mono16, [150, 0]), gain, -100 * gain, PixelFormat.Mono8));
        Assert.Equal(128, Assert.Single(other.Image.Pixels.ToArray()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayLinearOperation(image, double.PositiveInfinity, 0, PixelFormat.Mono8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayLinearOperation(image, gain, 0, PixelFormat.Bgr24));
        await Unsupported(new ColorChannelExtractOperation(image, RgbChannel.Red));
    }

    [Fact]
    public async Task ExplicitIdentityGrayTransform_ReturnsIndependentCompactSnapshot()
    {
        var bytes = new byte[] { 1, 2, 88, 3, 4, 88 };
        var image = Frame(2, 2, PixelFormat.Mono8, bytes, 3);
        var result = await Run(new GrayLinearOperation(image, 1, 0, PixelFormat.Mono8));
        Array.Clear(bytes);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Image.Pixels.ToArray());
        Assert.Equal(2, result.Image.Stride);
        AssertTransformSource(image, result);
    }

    [Theory]
    [InlineData(ImageFilterKind.Mean)]
    [InlineData(ImageFilterKind.Gaussian)]
    [InlineData(ImageFilterKind.Median)]
    public async Task FilterImpulse_MatchesIndependentKernelAndDoesNotChangeInput(ImageFilterKind kind)
    {
        byte[] pixels = [0, 0, 0, 0, 128, 0, 0, 0, 0];
        var image = Frame(3, 3, PixelFormat.Mono8, pixels);
        var result = await Run(new ImageFilterOperation(image, kind, 3,
            kind == ImageFilterKind.Median ? ImageBorderMode.Replicate : ImageBorderMode.ConstantZero,
            kind == ImageFilterKind.Gaussian ? 1 : null));
        // 均值128/9；sigma=1高斯核按exp(-x²/2)归一化；九点中值为0。保留原独立期望。
        byte[] expected = kind switch
        {
            ImageFilterKind.Mean => [14, 14, 14, 14, 14, 14, 14, 14, 14],
            ImageFilterKind.Gaussian => [10, 16, 10, 16, 26, 16, 10, 16, 10],
            _ => new byte[9]
        };
        Assert.Equal(expected, result.Image.Pixels.ToArray());
        Assert.Equal(128, pixels[4]);
        AssertTransformSource(image, result);
    }

    [Fact]
    public async Task FilterBorders_AreExplicit_AndInvalidSettingsNeverProduceImages()
    {
        var image = Frame(1, 1, PixelFormat.Mono8, [90, 99], 2);
        foreach (var border in new[] { ImageBorderMode.Replicate, ImageBorderMode.Reflect101, ImageBorderMode.ConstantZero })
        {
            var result = await Run(new ImageFilterOperation(image, ImageFilterKind.Mean, 3, border));
            Assert.Equal(border == ImageBorderMode.ConstantZero ? 10 : 90, Assert.Single(result.Image.Pixels.ToArray()));
        }

        await Unsupported(new ImageFilterOperation(image, ImageFilterKind.Median, 3, ImageBorderMode.ConstantZero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageFilterOperation(image, ImageFilterKind.Gaussian, 4, ImageBorderMode.Replicate, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageFilterOperation(image, ImageFilterKind.Gaussian, 3, ImageBorderMode.Replicate, double.NaN));
    }

    [Theory]
    [InlineData(PixelFormat.Mono8)]
    [InlineData(PixelFormat.Mono16)]
    public async Task Statistics_UsesRawGrayLevelsMedianAndPopulationDeviation_WithPaddedRows(PixelFormat format)
    {
        int[] values = format == PixelFormat.Mono8 ? [0, 2, 4, 6] : [0, 20000, 40000, 60000];
        var bpp = format == PixelFormat.Mono8 ? 1 : 2;
        var stride = 2 * bpp + 1;
        var pixels = Enumerable.Repeat((byte)99, 2 * stride).ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            var offset = index / 2 * stride + index % 2 * bpp;
            if (bpp == 2)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(offset, 2), (ushort)values[index]);
            }
            else
            {
                pixels[offset] = (byte)values[index];
            }
        }

        var image = Frame(2, 2, format, pixels, stride);
        var statistics = await Run(new GrayStatisticsOperation(image));
        var histogram = await Run(new GrayHistogramOperation(image));
        var scale = format == PixelFormat.Mono8 ? 1 : 10000;
        Assert.Equal(4, statistics.PixelCount);
        Assert.Equal(0, statistics.Minimum);
        Assert.Equal(6 * scale, statistics.Maximum);
        Assert.Equal(3 * scale, statistics.Mean);
        Assert.Equal(3 * scale, histogram.Median);
        Assert.Equal(Math.Sqrt(5) * scale, statistics.PopulationStandardDeviation, 7);
        Assert.Equal(format == PixelFormat.Mono8 ? 256 : 65536, histogram.Bins.Count);
        Assert.Equal(4, histogram.Bins.Sum());
        Assert.All(values, value => Assert.Equal(1, histogram.Bins[value]));
    }

    [Fact]
    public async Task HistogramGrouping_DoesNotQuantizeMeanMedianOrDeviation_AndValidatesWidth()
    {
        var image = Frame(4, 1, PixelFormat.Mono8, [0, 1, 2, 3]);
        var histogram = await Run(new GrayHistogramOperation(image, 2));
        var statistics = await Run(new GrayStatisticsOperation(image));
        Assert.Equal(128, histogram.Bins.Count);
        Assert.Equal(2, histogram.BinWidth);
        Assert.Equal(2, histogram.Bins[0]);
        Assert.Equal(2, histogram.Bins[1]);
        Assert.Equal(1.5, statistics.Mean);
        Assert.Equal(1.5, histogram.Median);
        Assert.Equal(Math.Sqrt(1.25), statistics.PopulationStandardDeviation, 8);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayHistogramOperation(image, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayHistogramOperation(image, 65537));
        var invalid = await _engine.ExecuteAsync(_context, new GrayHistogramOperation(image, 257), default);
        Assert.Equal(VisionExecutionStatus.Failed, invalid.Status);
        Assert.Null(invalid.Value);
    }

    [Fact]
    public async Task Statistics_ReusesExactRoi_AndDistinguishesEmptyRegionFromBlackPixels()
    {
        var image = Frame(3, 3, PixelFormat.Mono8, [99, 0, 99, 0, 0, 0, 99, 0, 99]);
        var circle = new SearchRegion(new Roi { Kind = RoiKind.Circle, Pose = new(1, 1, 0), Size = new(2, 2) });
        var statistics = await Run(new GrayStatisticsOperation(image, circle));
        var histogram = await Run(new GrayHistogramOperation(image, region: circle));
        Assert.Equal(5, statistics.PixelCount);
        Assert.Equal(0, statistics.Mean);
        Assert.Equal(0, histogram.Median);
        Assert.Equal(0, statistics.PopulationStandardDeviation);
        Assert.Equal(5, histogram.Bins[0]);
        var rectangle = new SearchRegion(new Roi { Kind = RoiKind.Rectangle, Pose = new(0.5, 0.5, 0), Size = new(1, 1) });
        var single = await Run(new GrayStatisticsOperation(image, rectangle));
        Assert.Equal(1, single.PixelCount);
        Assert.Equal(99, single.Mean);
        var outside = new SearchRegion(new Roi { Kind = RoiKind.Rectangle, Pose = new(50, 50, 0), Size = new(2, 2) });
        Assert.Throws<ArgumentException>(() => new GrayStatisticsOperation(image, outside));
        Assert.Throws<ArgumentException>(() => new GrayHistogramOperation(image, region: outside));
    }

    [Fact]
    public async Task UnsupportedStorageAndMalformedBuffers_AreRejectedBeforeImplicitConversion()
    {
        var floating = Frame(1, 1, PixelFormat.Float32, [0, 0, 0, 0]);
        await Unsupported(new ImageConvertOperation(floating, PixelFormat.Mono8));
        await Unsupported(new ImageFilterOperation(floating, ImageFilterKind.Mean, 3, ImageBorderMode.Replicate));
        await Unsupported(new GrayStatisticsOperation(floating));
        await Unsupported(new ColorChannelExtractOperation(floating, RgbChannel.Blue));
        Assert.Throws<ArgumentException>(() => Frame(2, 2, PixelFormat.Mono8, [1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorChannelExtractOperation(floating, (RgbChannel)99));
        await Unsupported(new GrayStatisticsOperation(Frame(1, 1, PixelFormat.Bgr24, [1, 2, 3])));
    }

    [Fact]
    public async Task ImmutableRequestsFreezePixelsParametersAndRoi_AndOutliveProducerContext()
    {
        var pixels = Enumerable.Repeat((ushort)150, 9).SelectMany(BitConverter.GetBytes).ToArray();
        var image = Frame(3, 3, PixelFormat.Mono16, pixels);
        var gain = 255d / 100;
        var request = new GrayLinearOperation(image, gain, -100 * gain, PixelFormat.Mono8);
        gain = 1;
        Array.Clear(pixels);
        Assert.NotEqual(gain, request.Gain);
        var converted = await Run(request);
        Assert.All(converted.Image.Pixels.ToArray(), pixel => Assert.Equal(128, pixel));
        var filtering = new ImageFilterOperation(converted.Image, ImageFilterKind.Median, 3, ImageBorderMode.Replicate);
        var roi = new Roi { Kind = RoiKind.Rectangle, Pose = new(0.5, 0.5, 0), Size = new(1, 1) };
        var region = new SearchRegion(roi);
        var counting = new GrayStatisticsOperation(converted.Image, region);
        var binning = new GrayHistogramOperation(converted.Image, region: region);
        roi.Pose = new(100, 100, 0);
        await _engine.ReleaseContextAsync(_context, default);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await _engine.ExecuteAsync(_context, request, default));
        _context = await _engine.CreateContextAsync(default);
        var filtered = await Run(filtering);
        Assert.All(filtered.Image.Pixels.ToArray(), pixel => Assert.Equal(128, pixel));
        var statistics = await Run(counting);
        var histogram = await Run(binning);
        Assert.Equal(1, statistics.PixelCount);
        Assert.Equal(128, statistics.Mean);
        Assert.Equal(256, histogram.Bins.Count);
        Assert.Equal(1, histogram.Bins[128]);
    }

    private async Task<T> Run<T>(VisionOperation<T> operation) where T : class
    {
        var result = await _engine.ExecuteAsync(_context, operation, default);
        Assert.True(result.Status == VisionExecutionStatus.Success, result.Issue?.Message ?? result.Status.ToString());
        return Assert.IsType<T>(result.Value);
    }

    private async Task Unsupported<T>(VisionOperation<T> operation) where T : class
    {
        var result = await _engine.ExecuteAsync(_context, operation, default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Value);
    }

    private static void AssertTransformSource(VisionFrame source, ImageTransformOutput output)
    {
        Assert.Equal(source.FrameId, output.FrameId);
        Assert.NotEqual(source.FrameId, output.Image.FrameId);
        Assert.Equal(source.CoordinateSpaceId, output.Image.CoordinateSpaceId);
        Assert.Equal((source.Width, source.Height), (output.Image.Width, output.Image.Height));
    }

    private static VisionFrame Frame(int width, int height, PixelFormat format, byte[] pixels, int stride = 0) =>
        new(Guid.NewGuid(), Guid.NewGuid(), width, height, format, pixels, stride);
}
