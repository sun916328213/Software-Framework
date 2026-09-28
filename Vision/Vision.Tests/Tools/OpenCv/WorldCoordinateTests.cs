// ─────────────────────────────────────────────────────────────────────────────
// P7 测试 —— OpenCV Tool 结果转统一坐标系（含 Bounds 映射）。
// 同一条解析链：像素 (110,120) → world (50,50)；框按列模缩放.
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Coordinate;
using Vision.Core.Geometry;
using Vision.Tools.OpenCv.Coordinate;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Measurement;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

/// <summary>OpenCV 结果坐标转换测试。</summary>
[Trait("Category", "Unit")]
public sealed class WorldCoordinateTests
{
    private static CoordinateChain TestChain() => new(
        new Pose2D(10, 20, 0),
        new AffineTransform2D(0.5, 0, 0, 0, 0.5, 0),
        Pose2D.Identity);

    [Fact]
    public void BlobResult_ToWorld_MapsCenterAndBounds()
    {
        var pixel = new BlobResult
        {
            Success = true,
            Items = [new BlobItem { Index = 1, Area = 600, Center = new Point2D(110, 120), Bounds = new Rect2D(100, 110, 20, 20) }],
        };
        var world = ResultCoordinates.ToWorld(pixel, TestChain());
        Assert.Equal(new Point2D(50, 50), world.Items[0].Center);
        // 框中心 (110,120)→(50,50)，尺寸 ×0.5。
        Assert.Equal(new Rect2D(45, 45, 10, 10), world.Items[0].Bounds);

        var machine = ResultCoordinates.ToMachine(pixel, TestChain());
        Assert.Equal(new Point2D(50, 50), machine.Items[0].Center);
        Assert.Equal(new Rect2D(45, 45, 10, 10), machine.Items[0].Bounds);
    }

    [Fact]
    public void FindLineResult_ToMachine_MapsEndpoints()
    {
        var pixel = new FindLineResult
        {
            Success = true,
            Start = new Point2D(110, 120),
            End = new Point2D(210, 220),
            EdgePointCount = 7,
        };
        var machine = ResultCoordinates.ToMachine(pixel, TestChain());
        Assert.Equal(new Point2D(50, 50), machine.Start);
        Assert.Equal(new Point2D(100, 100), machine.End);
    }
}
