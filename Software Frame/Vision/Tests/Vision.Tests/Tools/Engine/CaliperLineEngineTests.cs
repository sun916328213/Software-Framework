using System.Buffers.Binary;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class CaliperLineEngineTests
{
    [Theory]
    [InlineData(CaliperScanDirection.Forward, CaliperEdgeSelection.First, CaliperEdgePolarity.Any, 29.5, -180)]
    [InlineData(CaliperScanDirection.Forward, CaliperEdgeSelection.Last, CaliperEdgePolarity.Any, 11.5, -60)]
    [InlineData(CaliperScanDirection.Reverse, CaliperEdgeSelection.First, CaliperEdgePolarity.Any, 11.5, 60)]
    [InlineData(CaliperScanDirection.Reverse, CaliperEdgeSelection.Strongest, CaliperEdgePolarity.Any, 29.5, 180)]
    [InlineData(CaliperScanDirection.Forward, CaliperEdgeSelection.First, CaliperEdgePolarity.DarkToLight, 19.5, 40)]
    [InlineData(CaliperScanDirection.Reverse, CaliperEdgeSelection.First, CaliperEdgePolarity.LightToDark, 19.5, -40)]
    public async Task ScanDirectionPolarityAndSelection_ChooseDifferentActualEdges(
        CaliperScanDirection direction, CaliperEdgeSelection selection, CaliperEdgePolarity polarity,
        double expectedY, double expectedContrast)
    {
        var pixels = new byte[64 * 48];
        for (var y = 0; y < 48; y++)
        {
            pixels.AsSpan(y * 64, 64).Fill((byte)(y < 12 ? 20 : y < 20 ? 80 : y < 30 ? 40 : 220));
        }
        var frame = Gray(64, 48, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new CaliperLineOperation(frame, new(8, 22), new(56, 22), 7, 18, 0,
            direction, polarity, selection, 20, 0, 5, 0.1);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(CaliperLineFitState.Fitted, result.Value!.State);
        var line = result.Value.Line!;
        Assert.Equal(expectedY, line.Start.Y, 5);
        Assert.Equal(expectedY, line.End.Y, 5);
        Assert.Equal(7, line.PointCount);
        Assert.Equal(7, result.Value.Samples.Count);
        Assert.All(result.Value.Samples, sample =>
        {
            Assert.True(sample.UsedInFit);
            Assert.Equal(expectedY, sample.Edge!.Value.Y, 5);
            Assert.Equal(expectedContrast, sample.SignedContrast, 5);
        });
        Assert.InRange(line.RmsResidualPixels, 0, 1e-5);
        Assert.Equal(frame.FrameId, result.Value.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, result.Value.CoordinateSpaceId);
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(expectedY, result.Value.Line!.Start.Y, 5);
    }

    [Fact]
    public async Task SmoothingAndContrastThreshold_ReallyAffectDetection_AndNoMatchHasNoOldLine()
    {
        var frame = HorizontalStep(200);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var sharp = await engine.ExecuteAsync(context, Request(frame, sigma: 0, threshold: 100), default);
        var smooth = await engine.ExecuteAsync(context, Request(frame, sigma: 1, threshold: 100), default);
        var high = await engine.ExecuteAsync(context, Request(frame, sigma: 0, threshold: 201), default);
        Assert.Equal(VisionExecutionStatus.Success, sharp.Status);
        Assert.Equal(23.5, sharp.Value!.Line!.Start.Y, 5);
        foreach (var failed in new[] { smooth, high })
        {
            Assert.Equal(VisionExecutionStatus.NoMatch, failed.Status);
            Assert.Null(failed.Value!.Line);
            Assert.Equal(CaliperLineFitState.InsufficientEdges, failed.Value.State);
            Assert.All(failed.Value.Samples, sample => Assert.Null(sample.Edge));
        }
    }

    [Fact]
    public async Task TangentAveragingAndResidualRejection_PreserveDiagnostics_AndMinimumPointsIsEnforced()
    {
        var baseFrame = HorizontalStep(200);
        var pixels = baseFrame.Pixels.ToArray();
        pixels[16 * 64 + 32] = 255;
        var frame = Gray(64, 48, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var narrow = await engine.ExecuteAsync(context, Request(frame, width: 0, minimumPoints: 5), default);
        var wide = await engine.ExecuteAsync(context, Request(frame, width: 1, minimumPoints: 7), default);
        var strict = await engine.ExecuteAsync(context, Request(frame, width: 0, minimumPoints: 7), default);
        Assert.Equal(VisionExecutionStatus.Success, narrow.Status);
        Assert.Equal(6, narrow.Value!.Line!.PointCount);
        var rejected = Assert.Single(narrow.Value.Samples.Where(sample => sample.Edge != null && !sample.UsedInFit));
        Assert.Equal(3, rejected.Index);
        Assert.True(rejected.ResidualPixels > 6);
        Assert.Equal(VisionExecutionStatus.Success, wide.Status);
        Assert.Equal(7, wide.Value!.Line!.PointCount);
        Assert.All(wide.Value.Samples, sample => Assert.True(sample.UsedInFit));
        Assert.Equal(VisionExecutionStatus.NoMatch, strict.Status);
        Assert.Equal(CaliperLineFitState.InsufficientInliers, strict.Value!.State);
        Assert.Null(strict.Value.Line);
    }

    [Fact]
    public async Task ObliqueCalipers_UseRotatedSampling_NotAnAxisAlignedWholeImageEdge()
    {
        var pixels = new byte[80 * 64];
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 80; x++)
            {
                pixels[y * 80 + x] = y >= 12 + 0.25 * x ? (byte)220 : (byte)0;
            }
        }
        var frame = Gray(80, 64, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new CaliperLineOperation(frame, new(8, 14), new(72, 30),
            9, 8, 1, minimumContrast: 50, gaussianSigmaPixels: 0, minimumPoints: 6, maximumResidualPixels: 1);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var line = result.Value!.Line!;
        var slope = (line.End.Y - line.Start.Y) / (line.End.X - line.Start.X);
        Assert.InRange(slope, 0.23, 0.27);
        Assert.InRange(line.RmsResidualPixels, 0, 0.6);
        Assert.InRange(line.MaximumResidualPixels, 0, 1);
        Assert.True(line.End.X > line.Start.X);
    }

    [Fact]
    public async Task Mono16_PreservesOriginalContrastUnits_AndIgnoresPadding()
    {
        var pixels = new byte[40 * 100];
        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 48; x++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(y * 100 + 2 * x, 2), y >= 20 ? (ushort)12000 : (ushort)0);
            }
            pixels.AsSpan(y * 100 + 96, 4).Fill(255);
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 48, 40, PixelFormat.Mono16, pixels, 100);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new CaliperLineOperation(frame,
            new(6, 20), new(42, 20), 7, 10, 0, minimumContrast: 5000, gaussianSigmaPixels: 0), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(19.5, result.Value!.Line!.Start.Y, 5);
        Assert.All(result.Value.Samples, sample => Assert.Equal(-12000, sample.SignedContrast, 4));
        Assert.Equal(pixels, frame.Pixels.ToArray());
    }

    [Fact]
    public void InvalidGeometryAndBudgets_AreRejectedBeforeSdkSampling()
    {
        var frame = HorizontalStep(200);
        Assert.Throws<ArgumentException>(() => new CaliperLineOperation(frame, new(20, 20), new(20, 20)));
        Assert.Throws<ArgumentException>(() => new CaliperLineOperation(frame, new(0, 3), new(63, 3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaliperLineOperation(frame, new(8, 20), new(56, 20), gaussianSigmaPixels: 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaliperLineOperation(frame, new(8, 20), new(56, 20), gaussianSigmaPixels: 0.001));
        Assert.Throws<ArgumentException>(() => new CaliperLineOperation(frame, new(8, 20), new(56, 20), 512, 512, 32));
    }

    private static CaliperLineOperation Request(VisionFrame frame, double sigma = 0, double threshold = 100,
        int width = 0, int minimumPoints = 5)
    {
        return new(frame, new(8, 24), new(56, 24), 7, 12, width,
            minimumContrast: threshold, gaussianSigmaPixels: sigma, minimumPoints: minimumPoints);
    }

    private static VisionFrame HorizontalStep(byte gray)
    {
        var pixels = new byte[64 * 48];
        pixels.AsSpan(24 * 64).Fill(gray);
        return Gray(64, 48, pixels);
    }

    private static VisionFrame Gray(int width, int height, byte[] pixels)
    {
        return new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);
    }
}
