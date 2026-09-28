// ─────────────────────────────────────────────────────────────────────────────
// P7 测试 —— ROI 跟随 Fixture（验收项之一）+ ROI 仿射映射 + 几何正确性。
// 刚体跟随必须精确；相似/非相似映射按 Roi 文件头的诚实口径断言形状变化.
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using Vision.Core.Coordinate;
using Vision.Core.Geometry;
using Vision.Core.Roi;
using Xunit;

namespace Vision.Tests.Core;

/// <summary>ROI 跟随与映射测试。</summary>
[Trait("Category", "Unit")]
public sealed class RoiFollowTests
{
    [Fact]
    public void FollowFixture_Translation_MovesRectExactly()
    {
        var roi = Roi.Rectangle(10, 10, 20, 10);
        var moved = roi.FollowFixture(new Pose2D(0, 0, 0), new Pose2D(5, 0, 0));
        Assert.Equal(RoiKind.Rectangle, moved.Kind);
        Assert.Equal(15, moved.Pose.X);
        Assert.Equal(10, moved.Pose.Y);
        Assert.Equal(roi.Size, moved.Size);
        Assert.Equal(new Rect2D(5, 5, 20, 10), moved.Bounds);
    }

    [Fact]
    public void FollowFixture_Rotation_TurnsRectIntoRotatedRect()
    {
        var roi = Roi.Rectangle(10, 0, 20, 10);
        var moved = roi.FollowFixture(new Pose2D(0, 0, 0), new Pose2D(0, 0, 90));
        Assert.Equal(RoiKind.RotatedRectangle, moved.Kind);
        Assert.True(moved.Pose.X < 1e-9, $"x={moved.Pose.X}");
        Assert.Equal(10, moved.Pose.Y, 9);
        Assert.Equal(90, Pose2D.NormalizeAngle(moved.Pose.AngleDeg), 9);
    }

    [Fact]
    public void FollowFixture_MovesPolygonVerticesExactly()
    {
        var roi = Roi.Polygon(new List<Point2D> { new(0, 0), new(10, 0), new(0, 10) });
        var moved = roi.FollowFixture(new Pose2D(0, 0, 0), new Pose2D(100, 200, 0));
        Assert.Equal(RoiKind.Polygon, moved.Kind);
        Assert.Equal(new Point2D(100, 200), moved.Outline[0]);
        Assert.Equal(new Point2D(110, 200), moved.Outline[1]);
        Assert.Equal(new Point2D(100, 210), moved.Outline[2]);
        // Pose/Size 重算为新外接框。
        Assert.Equal(105, moved.Pose.X);
        Assert.Equal(205, moved.Pose.Y);
    }

    [Fact]
    public void FollowFixture_KeepsCircleCircular()
    {
        var roi = Roi.Circle(10, 10, 20);
        var moved = roi.FollowFixture(new Pose2D(0, 0, 0), new Pose2D(0, 0, 45));
        Assert.Equal(RoiKind.Circle, moved.Kind);
        Assert.Equal(0, moved.Pose.AngleDeg);
        // 圆心 R45(10,10) = (0, 10√2)，直径不变。
        Assert.Equal(0, moved.Pose.X, 9);
        Assert.Equal(10 * Math.Sqrt(2), moved.Pose.Y, 9);
        Assert.Equal(new Size2D(20, 20), moved.Size);
    }

    [Fact]
    public void Transform_Similarity_KeepsRectShape()
    {
        var roi = Roi.Rectangle(10, 10, 20, 10);
        var scaled = roi.Transform(new AffineTransform2D(2, 0, 0, 0, 2, 0));
        Assert.Equal(RoiKind.Rectangle, scaled.Kind);
        Assert.Equal(new Size2D(40, 20), scaled.Size);

        var rotated = roi.Transform(AffineTransform2D.FromPose(new Pose2D(0, 0, 90)));
        Assert.Equal(RoiKind.RotatedRectangle, rotated.Kind);
    }

    [Fact]
    public void Transform_Shear_DegradesRectToPolygon()
    {
        var roi = Roi.Rectangle(0, 0, 20, 10);
        var sheared = roi.Transform(new AffineTransform2D(1, 0.5, 0, 0, 1, 0));
        Assert.Equal(RoiKind.Polygon, sheared.Kind);
        Assert.Equal(4, sheared.Outline.Count);
        // 角点 (-10,-5) → (-12.5,-5)。
        Assert.True(sheared.Outline[0].DistanceTo(new Point2D(-12.5, -5)) < 1e-9);
    }

    [Fact]
    public void Transform_NonUniformScale_TurnsCircleIntoEllipse()
    {
        var roi = Roi.Circle(0, 0, 10);
        var stretched = roi.Transform(new AffineTransform2D(2, 0, 0, 0, 1, 0));
        Assert.Equal(RoiKind.Ellipse, stretched.Kind);
        Assert.Equal(new Size2D(20, 10), stretched.Size);
    }

    [Fact]
    public void Bounds_OfCircle_AndJsonRoundTrip()
    {
        var roi = Roi.Circle(0, 0, 10);
        Assert.Equal(new Rect2D(-5, -5, 10, 10), roi.Bounds);
        Assert.Equal(24, roi.Outline.Count);
        Assert.False(roi.IsEmpty);
        Assert.True(new Roi().IsEmpty);

        var back = JsonSerializer.Deserialize<Roi>(JsonSerializer.Serialize(roi))!;
        Assert.Equal(RoiKind.Circle, back.Kind);
        Assert.Equal(roi.Pose, back.Pose);
        Assert.Equal(roi.Size, back.Size);
    }
}
