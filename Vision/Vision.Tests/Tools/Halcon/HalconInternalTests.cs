// ─────────────────────────────────────────────────────────────────────────────
// P3 验收测试 —— Internal 纯逻辑 + Fixture 纯数学路径（普通 CI）。
// HalconHelper 的矩阵串/阈值校验、FixtureTool.BuildPose 都不碰 HALCON Runtime。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Geometry;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Internal;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

/// <summary>P3 Internal helper 与纯数学路径测试。</summary>
[Trait("Category", "Unit")]
public sealed class HalconInternalTests
{
    [Fact]
    public void MatrixString_RoundTrip()
    {
        var values = new double[] { 1, 0, 10, 0, 1, 20, 0, 0, 1 };
        var text = HalconHelper.ToMatrixString(values);
        Assert.Equal("1.000,0.000,10.000|0.000,1.000,20.000|0.000,0.000,1.000", text);
        Assert.Equal(values, HalconHelper.ParseMatrixString(text));
    }

    [Fact]
    public void MatrixString_AcceptsLegacyCommaOnlyFormat()
    {
        // 旧 HTool_Fixtrue.Matrix 默认值写法："1, 0, 0, 0, 1, 0, 0, 0, 1"
        var values = HalconHelper.ParseMatrixString("1, 0, 0, 0, 1, 0, 0, 0, 1");
        Assert.Equal(9, values.Length);
        Assert.True(HalconHelper.IsIdentityMatrix("1, 0, 0, 0, 1, 0, 0, 0, 1"));
        Assert.True(HalconHelper.IsIdentityMatrix(HalconHelper.IdentityMatrixString));
        Assert.False(HalconHelper.IsIdentityMatrix("1.000,0.000,5.000|0.000,1.000,0.000|0.000,0.000,1.000"));
    }

    [Fact]
    public void MatrixString_BadInput_FailsWithoutThrowing()
    {
        Assert.False(HalconHelper.TryParseMatrixString("1,2,3", out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.False(HalconHelper.TryParseMatrixString(null, out _, out _));
        Assert.Throws<FormatException>(() => HalconHelper.ParseMatrixString("not a matrix"));
    }

    [Theory]
    [InlineData(0, 255, true)]
    [InlineData(200, 100, false)]
    [InlineData(-1, 100, false)]
    [InlineData(0, 256, false)]
    public void ThresholdRange_Validation(int min, int max, bool expected)
    {
        Assert.Equal(expected, HalconHelper.IsValidThresholdRange(min, max, out _));
    }

    [Fact]
    public void Fixture_BuildPose_Identity()
    {
        var parameters = new FixtureParameters
        {
            BasePoint = new Point2D(10, 20),
            TargetPoint = new Point2D(10, 20),
            RotationDeg = 0,
            Scale = 1,
        };
        var result = FixtureTool.BuildPose(parameters);
        Assert.True(result.Success);
        Assert.True(HalconHelper.IsIdentityMatrix(result.Matrix), $"期望单位矩阵，实际 {result.Matrix}");
        Assert.Equal(0, result.Pose.AngleDeg);
    }

    [Fact]
    public void Fixture_BuildPose_PureTranslation()
    {
        var parameters = new FixtureParameters
        {
            BasePoint = new Point2D(0, 0),
            TargetPoint = new Point2D(10, 20),
            RotationDeg = 0,
            Scale = 1,
        };
        var result = FixtureTool.BuildPose(parameters);
        Assert.Equal("1.000,0.000,10.000|0.000,1.000,20.000|0.000,0.000,1.000", result.Matrix);
    }

    [Fact]
    public void Fixture_BuildPose_NinetyDegreeRotation()
    {
        var parameters = new FixtureParameters
        {
            BasePoint = new Point2D(0, 0),
            TargetPoint = new Point2D(0, 0),
            RotationDeg = 90,
            Scale = 1,
        };
        var result = FixtureTool.BuildPose(parameters);
        var values = HalconHelper.ParseMatrixString(result.Matrix);
        Assert.Equal(0, values[0], 3);
        Assert.Equal(-1, values[1], 3);
        Assert.Equal(1, values[3], 3);
        Assert.Equal(0, values[4], 3);
    }

    [Fact]
    public void Fixture_BuildPose_NonPositiveScale_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FixtureTool.BuildPose(new FixtureParameters { Scale = 0 }));
    }
}
