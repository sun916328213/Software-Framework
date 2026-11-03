using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class BlobContourMetricsTests
{
    [Theory]
    [InlineData(BlobFeature.ContourArea, 3.9, 4.1)]
    [InlineData(BlobFeature.ContourPerimeter, 7.9, 8.1)]
    [InlineData(BlobFeature.ContourCircularity, 0.78, 0.79)]
    public async Task ContourFeatureFilters_SelectSquare_WithoutConfusingPixelAreaOrDegenerateLine(
        BlobFeature feature, double minimum, double maximum)
    {
        var pixels = new byte[12 * 8];
        for (var y = 1; y <= 3; y++)
        {
            pixels.AsSpan(y * 12 + 1, 3).Fill(255);
        }
        pixels.AsSpan(5 * 12 + 6, 4).Fill(255);
        var frame = Gray(12, 8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context,
            new BlobOperation(frame, 255, 255, filters: [new(feature, minimum, maximum)]), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var square = Assert.Single(result.Value!.Items);
        Assert.Equal(9, square.Area);
        Assert.Equal(4, square.ContourMetrics.Area, 12);
        Assert.Equal(8, square.ContourMetrics.Perimeter, 12);
        Assert.Equal(Math.PI / 4, square.ContourMetrics.Circularity!.Value, 12);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 6)]
    public async Task DegenerateContours_KeepRealLength_AndUndefinedCircularityNeverMatchesZero(int length, double perimeter)
    {
        var pixels = new byte[8 * 5];
        pixels.AsSpan(2 * 8 + 2, length).Fill(255);
        var frame = Gray(8, 5, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(frame, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(length, item.Area);
        Assert.Equal(0, item.ContourMetrics.Area);
        Assert.Equal(perimeter, item.ContourMetrics.Perimeter);
        Assert.Null(item.ContourMetrics.Circularity);
        var zero = await engine.ExecuteAsync(context, new BlobOperation(frame, 255, 255,
            filters: [new(BlobFeature.ContourCircularity, 0, 0)]), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, zero.Status);
        Assert.Empty(zero.Value!.Items);
        BlobFeatureRange[] filters = [new(BlobFeature.Area, length, length), new(BlobFeature.ContourCircularity, 0, 1)];
        var all = await engine.ExecuteAsync(context, new BlobOperation(frame, 255, 255,
            filters: filters, filterLogic: BlobFilterLogic.All), default);
        var any = await engine.ExecuteAsync(context, new BlobOperation(frame, 255, 255,
            filters: filters, filterLogic: BlobFilterLogic.Any), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, all.Status);
        Assert.Equal(VisionExecutionStatus.Success, any.Status);
        Assert.Single(any.Value!.Items);
    }

    [Fact]
    public async Task HoleBoundary_ContributesLengthAndSubtractsArea_AndFillChangesBothMetrics()
    {
        var pixels = new byte[25];
        for (var y = 1; y <= 3; y++)
        {
            pixels.AsSpan(y * 5 + 1, 3).Fill(255);
        }
        pixels[2 * 5 + 2] = 0;
        var frame = Gray(5, 5, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var ring = await engine.ExecuteAsync(context, new BlobOperation(frame, 255, 255), default);
        var solid = await engine.ExecuteAsync(context,
            new BlobOperation(frame, 255, 255, holePolicy: BlobHolePolicy.Fill), default);
        Assert.Equal(VisionExecutionStatus.Success, ring.Status);
        Assert.Equal(VisionExecutionStatus.Success, solid.Status);
        var hollow = Assert.Single(ring.Value!.Items);
        var filled = Assert.Single(solid.Value!.Items);
        // 外部2×2方形减内部对角线为2的菱形；不调用SDK来生成期望。
        var expectedPerimeter = 8 + 4 * Math.Sqrt(2);
        Assert.Equal(8, hollow.Area);
        Assert.Equal(1, hollow.HoleCount);
        Assert.Equal(2, hollow.ContourMetrics.Area, 12);
        Assert.Equal(expectedPerimeter, hollow.ContourMetrics.Perimeter, 6);
        Assert.Equal(8 * Math.PI / (expectedPerimeter * expectedPerimeter), hollow.ContourMetrics.Circularity!.Value, 8);
        Assert.Equal(9, filled.Area);
        Assert.Equal(0, filled.HoleCount);
        Assert.Equal(4, filled.ContourMetrics.Area, 12);
        Assert.Equal(8, filled.ContourMetrics.Perimeter, 12);
        Assert.Equal(Math.PI / 4, filled.ContourMetrics.Circularity!.Value, 12);
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(2, hollow.ContourMetrics.Area);
        Assert.Equal(pixels, frame.Pixels.ToArray());
    }

    [Fact]
    public void ContourMetricContracts_RejectNonFiniteValues_AndDoNotEncodeUndefinedAsZero()
    {
        Assert.Throws<ArgumentException>(() => new BlobContourMetrics(1, double.NaN, 0.5));
        Assert.Throws<ArgumentException>(() => new BlobContourMetrics(0, 0, 0));
        Assert.Throws<ArgumentException>(() => new BlobContourMetrics(1, 1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlobFeatureRange(BlobFeature.ContourCircularity, 0, 1.01));
        Assert.Null(new BlobContourMetrics(0, 6, null).Circularity);
    }

    private static VisionFrame Gray(int width, int height, byte[] pixels)
    {
        return new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);
    }
}
