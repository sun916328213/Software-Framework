// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HalconInternalTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Core.Geometry、Vision.Core.Imaging、Vision.Tools.Halcon.Coordinate、Vision.Tools.Halcon.Internal、Xunit。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
// ─────────────────────────────────────────────────────────────────────────────
// P3 验收测试 —— Internal 纯逻辑 + Fixture 纯数学路径（普通 CI）。
// HalconHelper 的矩阵串/阈值校验、FixtureTool.BuildPose 都不碰 HALCON Runtime。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Internal;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

/// <summary>P3 Internal helper 与纯数学路径测试。</summary>
[Trait("Category", "Unit")]
public sealed class HalconInternalTests
{
    /// <summary>
    /// 测试相关入口 ImageConverter_RejectsPaddedRowsBeforeNativeCall；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、Throws、FromVisionImage；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ImageConverter_RejectsPaddedRowsBeforeNativeCall()
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 2, 0, 3, 4, 0], stride: 3);

        Assert.Throws<NotSupportedException>(() => HalconImageConverter.FromVisionImage(image));
    }

    /// <summary>
    /// 测试相关入口 MatrixString_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToMatrixString、Equal、ParseMatrixString；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MatrixString_RoundTrip()
    {
        var values = new double[] { 1, 0, 10, 0, 1, 20, 0, 0, 1 };
        var text = HalconHelper.ToMatrixString(values);
        Assert.Equal("1.000,0.000,10.000|0.000,1.000,20.000|0.000,0.000,1.000", text);
        Assert.Equal(values, HalconHelper.ParseMatrixString(text));
    }

    /// <summary>
    /// 测试相关入口 MatrixString_AcceptsLegacyCommaOnlyFormat；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ParseMatrixString、Equal、True、IsIdentityMatrix、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 MatrixString_BadInput_FailsWithoutThrowing；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 False、TryParseMatrixString、IsNullOrEmpty、Throws、ParseMatrixString；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MatrixString_BadInput_FailsWithoutThrowing()
    {
        Assert.False(HalconHelper.TryParseMatrixString("1,2,3", out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.False(HalconHelper.TryParseMatrixString(null, out _, out _));
        Assert.Throws<FormatException>(() => HalconHelper.ParseMatrixString("not a matrix"));
    }

    /// <summary>
    /// 测试相关入口 ThresholdRange_Validation；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、IsValidThresholdRange；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(0, 255, true)]
    [InlineData(200, 100, false)]
    [InlineData(-1, 100, false)]
    [InlineData(0, 256, false)]
    public void ThresholdRange_Validation(int min, int max, bool expected)
    {
        Assert.Equal(expected, HalconHelper.IsValidThresholdRange(min, max, out _));
    }

    /// <summary>
    /// 测试相关入口 Fixture_BuildPose_Identity；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildPose、True、IsIdentityMatrix、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Fixture_BuildPose_PureTranslation；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildPose、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Fixture_BuildPose_NinetyDegreeRotation；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildPose、ParseMatrixString、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Fixture_BuildPose_NonPositiveScale_Throws；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、BuildPose；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Fixture_BuildPose_NonPositiveScale_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FixtureTool.BuildPose(new FixtureParameters { Scale = 0 }));
    }
}
