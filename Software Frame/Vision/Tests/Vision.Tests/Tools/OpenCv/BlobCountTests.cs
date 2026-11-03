using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class BlobCountTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 3)]
    [InlineData(3, 10, 1)]
    public async Task CountUsesFinalFilteredComponents_AndEmptySnapshotIsValidZero(int shapes, int minimumArea, int expected)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(Image(shapes), 128, 255,
            filters: [new(BlobFeature.Area, minimumArea, 1000)]), default);
        Assert.Equal(expected == 0 ? VisionExecutionStatus.NoMatch : VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Items.Count);
        var count = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForCount(result.Value, expected, expected), default);
        Assert.Equal(VisionExecutionStatus.Success, count.Status);
        Assert.Equal(BlobJudgmentMetric.Count, count.Value!.Metric);
        Assert.Equal(expected, count.Value.ActualValue);
        Assert.True(count.Value.Passed);
    }

    [Fact]
    public async Task FeatureFilter_AppliesBeforeCount_NotRawLabels()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(Image(3), 128, 255,
            filters: [new(BlobFeature.Area, 9, 9)]), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(9, Assert.Single(result.Value!.Items).Area);
        var judgment = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForCount(result.Value, 1, 1), default);
        Assert.True(judgment.Value!.Passed);
        Assert.Equal(1, judgment.Value.ActualValue);
    }

    [Fact]
    public async Task RegionRestrictsCount_AndDisjointRegionIsInvalidNotFalseZero()
    {
        var image = Image(3);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var region = new VisionRegionBinding(new SearchRegion(Roi.Rectangle(5, 7, 10, 10)));
        var result = await engine.ExecuteAsync(context, new BlobOperation(image, 128, 255, region), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Single(result.Value!.Items);
        var outside = new VisionRegionBinding(new SearchRegion(Roi.Rectangle(100, 7, 10, 10)));
        Assert.Throws<ArgumentException>(() => new BlobOperation(image, 128, 255, outside));
    }

    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(3, 3, 3, true)]
    [InlineData(3, 0, 2, false)]
    [InlineData(3, 2.5, 3.5, true)]
    public async Task CountTolerance_UsesClosedRealBounds_AndNgIsSuccessful(int shapes, double minimum, double maximum, bool expected)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var blobs = await engine.ExecuteAsync(context, new BlobOperation(Image(shapes), 128, 255), default);
        Assert.Equal(shapes == 0 ? VisionExecutionStatus.NoMatch : VisionExecutionStatus.Success, blobs.Status);
        await engine.ReleaseContextAsync(context, default);
        var evaluation = await engine.CreateContextAsync(default);
        var request = BlobJudgmentOperation.ForCount(blobs.Value!, minimum, maximum);
        Assert.Null(request.Image);
        var result = await engine.ExecuteAsync(evaluation, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Passed);
        Assert.Equal(shapes, result.Value.ActualValue);
        Assert.Equal(minimum, result.Value.Minimum);
        Assert.Equal(maximum, result.Value.Maximum);
    }

    [Fact]
    public void MissingSourceAndInvalidBoundsCannotInventCountOrVerdict()
    {
        var source = new BlobOutput(Guid.NewGuid(), Guid.NewGuid(), []);
        Assert.Throws<ArgumentNullException>(() => BlobJudgmentOperation.ForCount(null!, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlobJudgmentOperation.ForCount(source, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlobJudgmentOperation.ForCount(source, 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlobJudgmentOperation.ForCount(source, 0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlobJudgmentOperation.ForCount(source, 0, double.PositiveInfinity));
    }

    private static VisionFrame Image(int shapes)
    {
        var pixels = new byte[64 * 40];
        for (var i = 0; i < shapes; i++)
        {
            for (var y = 5; y < 5 + i + 2; y++)
            {
                for (var x = 5 + i * 15; x < 5 + i * 15 + i + 2; x++)
                {
                    pixels[y * 64 + x] = 255;
                }
            }
        }
        return new(Guid.NewGuid(), Guid.NewGuid(), 64, 40, PixelFormat.Mono8, pixels);
    }
}
