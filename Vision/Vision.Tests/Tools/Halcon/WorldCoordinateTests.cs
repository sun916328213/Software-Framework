// ─────────────────────────────────────────────────────────────────────────────
// P7 测试 —— HALCON Tool 结果转统一坐标系（验收项之一）。
// 链由 P3 FixtureTool.BuildPose 的纯数学输出装配：base(0,0)→target(10,20)
// 得 fixture 姿态 (10,20,0°)；标定 0.5mm/px；机台恒等。
// 像素 (110,120) → fixture (100,100) → world (50,50) → 机台 (50,50)。
// 构造结果对象直测转换器，无需 Runtime.
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Coordinate;
using Vision.Core.Geometry;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Inspection;
using Vision.Tools.Halcon.Measurement;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

/// <summary>HALCON 结果坐标转换测试。</summary>
[Trait("Category", "Unit")]
public sealed class WorldCoordinateTests
{
    private static CoordinateChain TestChain()
    {
        var fixture = FixtureTool.BuildPose(new FixtureParameters
        {
            BasePoint = new Point2D(0, 0),
            TargetPoint = new Point2D(10, 20),
            RotationDeg = 0,
            Scale = 1,
        });
        return new CoordinateChain(
            fixture.Pose,
            new AffineTransform2D(0.5, 0, 0, 0, 0.5, 0),
            Pose2D.Identity);
    }

    [Fact]
    public void BlobResult_ToWorld_AndToMachine()
    {
        var pixel = new BlobResult
        {
            Success = true,
            Items = [new BlobItem { Index = 1, Area = 600, Center = new Point2D(110, 120) }],
        };
        var world = ResultCoordinates.ToWorld(pixel, TestChain());
        Assert.True(world.Success);
        Assert.Equal(new Point2D(50, 50), world.Items[0].Center);
        Assert.Equal(600, world.Items[0].Area);

        var machine = ResultCoordinates.ToMachine(pixel, TestChain());
        Assert.Equal(new Point2D(50, 50), machine.Items[0].Center);
    }

    [Fact]
    public void FindLineResult_ToWorld_MapsEndpoints()
    {
        var pixel = new FindLineResult
        {
            Success = true,
            Start = new Point2D(110, 120),
            End = new Point2D(210, 220),
            EdgePointCount = 42,
        };
        var world = ResultCoordinates.ToWorld(pixel, TestChain());
        Assert.Equal(new Point2D(50, 50), world.Start);
        Assert.Equal(new Point2D(100, 100), world.End);
        Assert.Equal(42, world.EdgePointCount);
    }

    [Fact]
    public void NonImageChain_IsRejected()
    {
        var pixel = new BlobResult { Items = [new BlobItem { Center = new Point2D(1, 1) }] };
        var bad = new CoordinateChain(
            new List<ICoordinateTransform>
            {
                new RigidTransform(CoordinateSpace.Fixture, CoordinateSpace.Calibration, Pose2D.Identity),
            });
        Assert.Throws<ArgumentException>(() => ResultCoordinates.ToWorld(pixel, bad));
    }
}
