using System.Diagnostics;
using HalconDotNet;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;
using Xunit.Abstractions;

namespace Vision.Tests.Tools.Engine;

// 仅经授权按类名运行：真实HALCON，内存合成图，不开相机/运动/文件图，不加载AI模型。
[Trait("Category", "RequiresRuntime")]
public sealed class HalconEngineSyntheticRuntimeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeThreshold_PreservesMono8AndMono16BoundsAndStride(bool mono16)
    {
        await using var engine = VisionEngineFactory.Create(new("halcon"));
        ReportRuntime();
        var context = await engine.CreateContextAsync(default);
        var input = ThresholdRangeEngineTests.Frame(mono16);
        var before = input.Pixels.ToArray();
        var result = await engine.ExecuteAsync(context, new ThresholdRangeOperation(input,
            (ushort)(mono16 ? 1000 : 10), (ushort)(mono16 ? 2000 : 20)), default);
        Assert.True(result.Status == VisionExecutionStatus.Success, result.Issue?.Message);
        Assert.Equal(new byte[] { 0, 255, 255, 255, 255, 0 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(before, input.Pixels.ToArray());
        Assert.Equal(input.CoordinateSpaceId, result.Value.Image.CoordinateSpaceId);
        Assert.NotEqual(input.FrameId, result.Value.Image.FrameId);
        await engine.ReleaseContextAsync(context, default);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(90, 1)]
    [InlineData(90, 2)]
    public async Task NativeShape_ReportsActualRotatedNonCentralAnchor_WithAsymmetricMask(int clockwiseDegrees, int scale)
    {
        var (template, mask) = Template();
        var input = Scene(template, clockwiseDegrees, scale);
        var origin = new Point2D(12, 17);
        await using var engine = VisionEngineFactory.Create(new("halcon"));
        ReportRuntime();
        var context = await engine.CreateContextAsync(default);
        var range = new TemplateSearchRange(0, 360, 0.8, 2.2);
        var model = await engine.PrepareModelAsync(context,
            new TemplateModelDefinition(template, origin, range, mask, TemplateMatchMetric.ShapeEdges), 1, default);
        var result = await engine.ExecuteAsync(context, new TemplateMatchOperation(input, model, range,
            minimumScore: 0.7, sampling: TemplateSearchSampling.NativeAutomatic), default);
        Assert.True(result.Status == VisionExecutionStatus.Success, result.Issue?.Message ?? result.Status.ToString());
        var found = Assert.Single(result.Value!.Matches);
        // 独立整数栅格构造：0度(u,v)，90度(H-1-v,u)，复制s倍像素的中心再偏移(s-1)/2。
        // 期望只由输入栅格和放置常量推导，不从SDK结果反算。
        var expected = clockwiseDegrees == 0 ? new Point2D(102, 87)
            : scale == 1 ? new Point2D(136, 82) : new Point2D(182.5, 94.5);
        output.WriteLine($"angle={found.AngleDegrees:F6}; scale={found.Scale:F6}; anchor={found.Position}; expected={expected}; score={found.Score:F6}");
        Assert.InRange(found.Position.DistanceTo(expected), 0, 0.5);
        Assert.InRange(Math.Abs(Pose2D.NormalizeAngle(found.AngleDegrees - clockwiseDegrees)), 0, 0.5);
        Assert.InRange(found.Scale, scale - 0.01, scale + 0.01);
        Assert.Equal(origin, found.ModelOrigin);
        Assert.Equal(input.FrameId, result.Value.FrameId);
        await engine.ReleaseContextAsync(context, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeStatistics_PreservesPopulationDeviationAnd16BitValues(bool mono16)
    {
        await using var engine = VisionEngineFactory.Create(new("halcon"));
        ReportRuntime();
        var context = await engine.CreateContextAsync(default);
        var input = GrayStatisticsEngineTests.Frame(mono16);
        var result = await engine.ExecuteAsync(context, new GrayStatisticsOperation(input), default);
        Assert.True(result.Status == VisionExecutionStatus.Success, result.Issue?.Message ?? result.Status.ToString());
        GrayStatisticsEngineTests.AssertStatistics(input, result.Value!, mono16);
        var single = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Mono16, [255, 255]);
        var one = await engine.ExecuteAsync(context, new GrayStatisticsOperation(single), default);
        Assert.Equal(VisionExecutionStatus.Success, one.Status);
        Assert.Equal(65535, one.Value!.Mean); Assert.Equal(0, one.Value.PopulationStandardDeviation);
    }

    private void ReportRuntime()
    {
        HOperatorSet.GetSystem("version", out var version);
        using (version) output.WriteLine("HALCON version: " + version.S);
        var module = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
            .FirstOrDefault(item => item.ModuleName.Equals("halcon.dll", StringComparison.OrdinalIgnoreCase));
        output.WriteLine("Native module: " + (module?.FileName ?? "not reported"));
    }

    private static (VisionFrame Image, VisionMask Mask) Template()
    {
        const int width = 80, height = 64;
        var pixels = Enumerable.Repeat((byte)20, width * height).ToArray();
        var mask = new byte[pixels.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var value = (byte)20;
                if ((x >= 10 && x <= 20 && y >= 10 && y <= 54) ||
                    (x >= 20 && x <= 66 && y >= 44 && y <= 54)) value = 210;
                if (x >= 28 && x <= 43 && y >= 15 && y <= 25) value = 130;
                if (x >= 55 && x <= 65 && y >= 10 && y <= 20) value = 240;
                pixels[y * width + x] = value;
                mask[y * width + x] = x < 70 && y >= 6 && !(x >= 31 && x <= 38 && y >= 30 && y <= 38) ? (byte)255 : (byte)0;
            }
        return (new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels), new(width, height, mask));
    }

    private static VisionFrame Scene(VisionFrame template, int angle, int scale)
    {
        const int width = 256, height = 256, x0 = 90, y0 = 70;
        var pixels = Enumerable.Repeat((byte)20, width * height).ToArray();
        for (var y = 0; y < template.Height; y++)
            for (var x = 0; x < template.Width; x++)
            {
                var column = x0 + scale * (angle == 0 ? x : template.Height - 1 - y);
                var row = y0 + scale * (angle == 0 ? y : x);
                for (var dy = 0; dy < scale; dy++)
                    for (var dx = 0; dx < scale; dx++)
                        pixels[(row + dy) * width + column + dx] = template.Pixels[y * template.Stride + x];
            }
        return new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);
    }
}
