using System.Buffers.Binary;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class CaliperEdgePairEngineTests
{
    [Theory]
    [InlineData(CaliperPairPolarity.LightStripe, CaliperScanDirection.Forward, 27.5, 17.5, 200)]
    [InlineData(CaliperPairPolarity.LightStripe, CaliperScanDirection.Reverse, 17.5, 27.5, 200)]
    [InlineData(CaliperPairPolarity.DarkStripe, CaliperScanDirection.Forward, 27.5, 17.5, -200)]
    [InlineData(CaliperPairPolarity.DarkStripe, CaliperScanDirection.Reverse, 17.5, 27.5, -200)]
    public async Task OppositeEdges_ArePairedInsideEachScan_WithExplicitStripePolarityAndDirection(
        CaliperPairPolarity polarity, CaliperScanDirection direction, double firstY, double secondY, double firstContrast)
    {
        var pixels = new byte[64 * 48];
        for (var y = 0; y < 48; y++)
        {
            var inside = y >= 18 && y < 28;
            pixels.AsSpan(y * 64, 64).Fill(inside == (polarity == CaliperPairPolarity.LightStripe) ? (byte)200 : (byte)0);
        }
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var frame = Gray(64, 48, pixels);
        var result = await engine.ExecuteAsync(context, Request(frame, 10, 10, polarity: polarity, direction: direction), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(7, result.Value!.Statistics!.PairCount);
        Assert.Equal(10, result.Value.Statistics.Mean.Value);
        Assert.Equal(GeometryMeasurementKind.CaliperPairMeanWidth, result.Value.Statistics.Mean.Kind);
        Assert.Equal(GeometryMeasurementUnit.Pixels, result.Value.Statistics.Mean.Unit);
        Assert.All(result.Value.Samples, sample =>
        {
            Assert.Equal(firstY, sample.FirstEdge!.Value.Y);
            Assert.Equal(secondY, sample.SecondEdge!.Value.Y);
            Assert.Equal(firstContrast, sample.FirstContrast);
            Assert.Equal(-firstContrast, sample.SecondContrast);
            Assert.Equal(10, sample.WidthPixels);
        });
        var decision = await engine.ExecuteAsync(context, new MeasurementRangeOperation(result.Value.Statistics.Mean,
            9.9, 10.1, GeometryMeasurementUnit.Pixels), default);
        Assert.True(decision.Value!.Passed);
    }

    [Theory]
    [InlineData(CaliperScanDirection.Forward, CaliperEdgeSelection.First, 8)]
    [InlineData(CaliperScanDirection.Forward, CaliperEdgeSelection.Last, 5)]
    [InlineData(CaliperScanDirection.Reverse, CaliperEdgeSelection.First, 5)]
    [InlineData(CaliperScanDirection.Reverse, CaliperEdgeSelection.Strongest, 8)]
    public async Task MultipleStripes_UsePairSelection_NotIndependentStrongestEdges(
        CaliperScanDirection direction, CaliperEdgeSelection selection, double width)
    {
        var frame = TwoStripes();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(frame, 1, 20, direction: direction, selection: selection), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(width, result.Value!.Statistics!.Mean.Value);
        Assert.All(result.Value.Samples, sample =>
        {
            Assert.Equal(4, sample.QualifiedEdgeCount);
            Assert.Equal(2, sample.CandidatePairCount);
        });
    }

    [Fact]
    public async Task WidthRangeIsAppliedBeforeSelection_AndInsufficientPairsDoNotReuseOldStatistics()
    {
        var frame = TwoStripes();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var filtered = await engine.ExecuteAsync(context, Request(frame, 5, 5), default);
        var absent = await engine.ExecuteAsync(context, Request(frame, 9, 11), default);
        Assert.Equal(VisionExecutionStatus.Success, filtered.Status);
        Assert.Equal(5, filtered.Value!.Statistics!.Mean.Value);
        Assert.Equal(VisionExecutionStatus.NoMatch, absent.Status);
        Assert.Null(absent.Value!.Statistics);
        Assert.All(absent.Value.Samples, sample =>
        {
            Assert.Null(sample.WidthPixels);
            Assert.Null(sample.FirstEdge);
            Assert.Null(sample.SecondEdge);
            Assert.Equal(4, sample.QualifiedEdgeCount);
            Assert.Equal(0, sample.CandidatePairCount);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => Request(frame, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Request(frame, 11, 10));
    }

    [Fact]
    public async Task EdgesOnDifferentProfiles_CannotBeCombinedIntoAnArtificialWidth()
    {
        var pixels = new byte[64 * 48];
        for (var y = 0; y < 48; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                pixels[y * 64 + x] = (x < 32) == (y >= 24) ? (byte)200 : (byte)0;
            }
        }
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(Gray(64, 48, pixels), 1, 20), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Null(result.Value!.Statistics);
        Assert.All(result.Value.Samples, sample =>
        {
            Assert.Equal(1, sample.QualifiedEdgeCount);
            Assert.Null(sample.WidthPixels);
        });
    }

    [Fact]
    public async Task WidthStatistics_IncludeAllCompletePairs_AndMinimumCountIsEnforced()
    {
        var pixels = new byte[64 * 48];
        foreach (var (x, width) in new[] { (8, 4), (32, 6), (56, 8) })
        {
            for (var y = 18; y < 18 + width; y++)
            {
                pixels[y * 64 + x] = 200;
            }
        }
        var frame = Gray(64, 48, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new CaliperEdgePairOperation(frame, new(8, 22), new(56, 22), 1, 15,
            caliperCount: 3, normalHalfLength: 16, tangentHalfWidth: 0, minimumContrast: 50, gaussianSigmaPixels: 0, minimumPairs: 3);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var stats = result.Value!.Statistics!;
        Assert.Equal(6, stats.Mean.Value);
        Assert.Equal(4, stats.MinimumWidthPixels);
        Assert.Equal(8, stats.MaximumWidthPixels);
        Assert.Equal(Math.Sqrt(8d / 3), stats.PopulationStandardDeviationPixels, 10);
        var incomplete = await engine.ExecuteAsync(context, new CaliperEdgePairOperation(frame,
            new(8, 22), new(56, 22), 1, 15, caliperCount: 7, normalHalfLength: 16, tangentHalfWidth: 0,
            minimumContrast: 50, gaussianSigmaPixels: 0, minimumPairs: 7), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, incomplete.Status);
        Assert.Null(incomplete.Value!.Statistics);
        Assert.Equal(3, incomplete.Value.Samples.Count(sample => sample.WidthPixels != null));
        await engine.ReleaseContextAsync(context, default);
        var evaluation = await engine.CreateContextAsync(default);
        var ng = await engine.ExecuteAsync(evaluation, new MeasurementRangeOperation(stats.Mean, 6.1, 7, GeometryMeasurementUnit.Pixels), default);
        Assert.False(ng.Value!.Passed);
    }

    [Fact]
    public async Task Mono16PairContrast_IsNotImplicitlyNormalizedToEightBits()
    {
        var pixels = new byte[48 * 48 * 2];
        for (var y = 18; y < 28; y++)
        {
            for (var x = 0; x < 48; x++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan((y * 48 + x) * 2, 2), 12000);
            }
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 48, 48, PixelFormat.Mono16, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new CaliperEdgePairOperation(frame,
            new(6, 22), new(42, 22), 10, 10, caliperCount: 5, normalHalfLength: 16,
            tangentHalfWidth: 0, minimumContrast: 5000, gaussianSigmaPixels: 0), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(10, result.Value!.Statistics!.Mean.Value);
        Assert.All(result.Value.Samples, sample => Assert.Equal(12000, sample.FirstContrast));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NarrowStripe_OppositeSignNeighborPeaksAreNotMergedIntoOnePlateau(int width)
    {
        var pixels = new byte[64 * 48];
        for (var y = 22; y < 22 + width; y++)
        {
            pixels.AsSpan(y * 64, 64).Fill(200);
        }
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(Gray(64, 48, pixels), width, width), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(width, result.Value!.Statistics!.Mean.Value);
        Assert.All(result.Value.Samples, sample => Assert.Equal(2, sample.QualifiedEdgeCount));
    }

    [Fact]
    public async Task StrongestPair_IsRankedByTheWeakerEdge_NotOneHighContrastBoundary()
    {
        var pixels = new byte[64 * 48];
        for (var y = 0; y < 48; y++)
        {
            var gray = y < 10 ? 0 : y < 15 ? 200 : y < 26 ? 180 : y < 34 ? 250 : 180;
            pixels.AsSpan(y * 64, 64).Fill((byte)gray);
        }
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(Gray(64, 48, pixels), 1, 20), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(8, result.Value!.Statistics!.Mean.Value);
        Assert.All(result.Value.Samples, sample =>
        {
            Assert.Equal(70, sample.FirstContrast);
            Assert.Equal(-70, sample.SecondContrast);
        });
    }

    private static CaliperEdgePairOperation Request(VisionFrame frame, double minimumWidth, double maximumWidth,
        CaliperPairPolarity polarity = CaliperPairPolarity.LightStripe,
        CaliperScanDirection direction = CaliperScanDirection.Forward,
        CaliperEdgeSelection selection = CaliperEdgeSelection.Strongest)
    {
        return new(frame, new(8, 22), new(56, 22), minimumWidth, maximumWidth, polarity, selection,
            7, 20, 0, direction, 20, 0, 5);
    }

    private static VisionFrame TwoStripes()
    {
        var pixels = new byte[64 * 48];
        for (var y = 10; y < 15; y++)
        {
            pixels.AsSpan(y * 64, 64).Fill(100);
        }
        for (var y = 26; y < 34; y++)
        {
            pixels.AsSpan(y * 64, 64).Fill(200);
        }
        return Gray(64, 48, pixels);
    }

    private static VisionFrame Gray(int width, int height, byte[] pixels)
    {
        return new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);
    }
}
