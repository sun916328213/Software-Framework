using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Regression")]
public sealed class UniqueBlobCenterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UniqueFinalBlob_ProvidesItsActualCenter(bool filtered)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new BlobOperation(Frame(filtered ? 2 : 1), 128, 255,
            filters: filtered ? [new(BlobFeature.Area, 30, 100)] : null);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Single(result.Value!.Items);
        var center = PixelPoint.FromUniqueBlobCenter(result.Value);
        Assert.Equal(new Point2D(12.5, 22.5), center.Position);
        Assert.Equal(result.Value.FrameId, center.FrameId);
        Assert.Equal(result.Value.CoordinateSpaceId, center.CoordinateSpaceId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task EmptyOrAmbiguousBlob_DoesNotInventOriginOrChooseFirst(int count)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(Frame(count), 128, 255), default);
        Assert.Equal(count == 0 ? VisionExecutionStatus.NoMatch : VisionExecutionStatus.Success, result.Status);
        Assert.Equal(count, result.Value!.Items.Count);
        Assert.Throws<ArgumentException>(() => PixelPoint.FromUniqueBlobCenter(result.Value));
        var verdict = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForCount(result.Value, count, count), default);
        Assert.True(verdict.Value!.Passed);
    }

    [Fact]
    public async Task RealOriginRemainsValid_AndPointSnapshotSurvivesContextRelease()
    {
        var pixels = new byte[64 * 40];
        pixels[0] = 255;
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 64, 40, PixelFormat.Mono8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(frame, 128, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var point = PixelPoint.FromUniqueBlobCenter(result.Value!);
        Assert.Equal(Point2D.Origin, point.Position);
        await engine.ReleaseContextAsync(context, default);
        Array.Clear(pixels);
        var evaluation = await engine.CreateContextAsync(default);
        var measured = await engine.ExecuteAsync(evaluation,
            new PointDistanceOperation(point, new PixelPoint(frame.FrameId, frame.CoordinateSpaceId, new(3, 4))), default);
        Assert.Equal(VisionExecutionStatus.Success, measured.Status);
        Assert.Equal(5, measured.Value!.Value);
    }

    [Fact]
    public void MissingSnapshotAndInvalidIndex_DoNotProducePoint()
    {
        Assert.Throws<ArgumentNullException>(() => PixelPoint.FromUniqueBlobCenter(null!));
        var empty = new BlobOutput(Guid.NewGuid(), Guid.NewGuid(), []);
        Assert.Throws<ArgumentException>(() => PixelPoint.FromUniqueBlobCenter(empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelPoint.FromBlobCenter(empty, 1));
    }

    private static VisionFrame Frame(int count)
    {
        var pixels = new byte[64 * 40];
        if (count >= 1)
        {
            for (var y = 20; y < 26; y++)
            {
                for (var x = 10; x < 16; x++)
                {
                    pixels[y * 64 + x] = 255;
                }
            }
        }
        if (count >= 2)
        {
            for (var y = 5; y < 10; y++)
            {
                for (var x = 40; x < 45; x++)
                {
                    pixels[y * 64 + x] = 255;
                }
            }
        }
        return new(Guid.NewGuid(), Guid.NewGuid(), 64, 40, PixelFormat.Mono8, pixels);
    }
}
