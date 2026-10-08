// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AffineTransformTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json、Vision.Core.Coordinate、Vision.Core.Geometry、Xunit。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P7 测试 —— AffineTransform2D（仿射数学、最小二乘标定、HALCON 矩阵串互操作）。
// 全部纯逻辑，无 Runtime 依赖.
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using Vision.Core.Coordinate;
using Vision.Core.Geometry;
using Xunit;

namespace Vision.Tests.Core;

/// <summary>仿射变换单元测试。</summary>
[Trait("Category", "Unit")]
public sealed class AffineTransformTests
{
    /// <summary>
    /// 测试相关入口 Identity_TransformsToSelf_AndInverts；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Transform、TransformInverse、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Identity_TransformsToSelf_AndInverts()
    {
        var point = new Point2D(3, -4);
        Assert.Equal(point, AffineTransform2D.Identity.Transform(point));
        Assert.Equal(point, AffineTransform2D.Identity.TransformInverse(point));
        Assert.True(AffineTransform2D.Identity.IsInvertible);
    }

    /// <summary>
    /// 测试相关入口 ScaleTranslate_MapsAndInverts；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Transform、TransformInverse、True、DistanceTo；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ScaleTranslate_MapsAndInverts()
    {
        var map = new AffineTransform2D(2, 0, 10, 0, 2, 20);
        Assert.Equal(new Point2D(20, 34), map.Transform(new Point2D(5, 7)));
        var back = map.TransformInverse(map.Transform(new Point2D(-3, 11)));
        Assert.True(back.DistanceTo(new Point2D(-3, 11)) < 1e-9);
    }

    /// <summary>
    /// 测试相关入口 Singular_ThrowsOnInverse；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 False、Throws、Inverse、TransformInverse；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Singular_ThrowsOnInverse()
    {
        var singular = new AffineTransform2D(1, 2, 0, 2, 4, 0); // 行列式 0。
        Assert.False(singular.IsInvertible);
        Assert.Throws<InvalidOperationException>(() => singular.Inverse());
        Assert.Throws<InvalidOperationException>(() => singular.TransformInverse(new Point2D(1, 1)));
    }

    /// <summary>
    /// 测试相关入口 Compose_AppliesChildFirst；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Compose、Equal、Transform；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Compose_AppliesChildFirst()
    {
        var child = new AffineTransform2D(2, 0, 0, 0, 2, 0); // ×2。
        var parent = new AffineTransform2D(1, 0, 5, 0, 1, 5); // +5。
        var composed = parent.Compose(child);
        Assert.Equal(parent.Transform(child.Transform(new Point2D(3, 4))), composed.Transform(new Point2D(3, 4)));
    }

    /// <summary>
    /// 测试相关入口 FromPose_RoundTrips_WithTryToPose；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromPose、True、TryToPose、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FromPose_RoundTrips_WithTryToPose()
    {
        var pose = new Pose2D(10, -20, 30);
        var affine = AffineTransform2D.FromPose(pose);
        Assert.True(affine.TryToPose(out var back));
        Assert.Equal(pose.X, back.X, 9);
        Assert.Equal(pose.Y, back.Y, 9);
        Assert.Equal(pose.AngleDeg, back.AngleDeg, 9);

        var scaled = new AffineTransform2D(2, 0, 0, 0, 2, 0);
        Assert.False(scaled.TryToPose(out _));
    }

    /// <summary>
    /// 测试相关入口 FromPointPairs_RecoversKnownAffine；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Cos、Sin、Add、Transform、FromPointPairs；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FromPointPairs_RecoversKnownAffine()
    {
        // 已知变换：旋转 30° + 缩放 2 + 平移(10,-5)。
        var rad = 30 * Math.PI / 180.0;
        var expected = new AffineTransform2D(2 * Math.Cos(rad), -2 * Math.Sin(rad), 10, 2 * Math.Sin(rad), 2 * Math.Cos(rad), -5);
        var from = new List<Point2D> { new(0, 0), new(10, 0), new(0, 10), new(5, 7) };
        var to = new List<Point2D>();
        foreach (var point in from)
            to.Add(expected.Transform(point));

        var recovered = AffineTransform2D.FromPointPairs(from, to);
        Assert.Equal(expected.A, recovered.A, 6);
        Assert.Equal(expected.B, recovered.B, 6);
        Assert.Equal(expected.Tx, recovered.Tx, 6);
        Assert.Equal(expected.C, recovered.C, 6);
        Assert.Equal(expected.D, recovered.D, 6);
        Assert.Equal(expected.Ty, recovered.Ty, 6);
    }

    /// <summary>
    /// 测试相关入口 FromPointPairs_RejectsBadInput；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、FromPointPairs、ToList、Take；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FromPointPairs_RejectsBadInput()
    {
        var ok = new List<Point2D> { new(0, 0), new(1, 0), new(0, 1) };
        Assert.Throws<ArgumentException>(() => AffineTransform2D.FromPointPairs(ok.Take(2).ToList(), ok.Take(2).ToList()));
        Assert.Throws<ArgumentException>(() => AffineTransform2D.FromPointPairs(ok, ok.Take(2).ToList()));
        // 共线三点：退化。
        var collinear = new List<Point2D> { new(0, 0), new(1, 1), new(2, 2) };
        Assert.Throws<InvalidOperationException>(() => AffineTransform2D.FromPointPairs(collinear, collinear));
    }

    /// <summary>
    /// 测试相关入口 HalconMatrixString_RoundTrips；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromHalconMatrixString、Equal、ToMatrixString、Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HalconMatrixString_RoundTrips()
    {
        const string text = "2.000,0.000,10.000|0.000,2.000,20.000|0.000,0.000,1.000";
        var parsed = AffineTransform2D.FromHalconMatrixString(text);
        Assert.Equal(2, parsed.A);
        Assert.Equal(10, parsed.Tx);
        Assert.Equal(20, parsed.Ty);
        Assert.Equal(text, parsed.ToMatrixString());
        // 兼容旧逗号写法。
        Assert.Equal(parsed, AffineTransform2D.FromHalconMatrixString("2, 0, 10, 0, 2, 20, 0, 0, 1"));
        // 非仿射末行拒绝。
        Assert.Throws<FormatException>(() => AffineTransform2D.FromHalconMatrixString("1,0,0|0,1,0|0,0,2"));
        Assert.Throws<FormatException>(() => AffineTransform2D.FromHalconMatrixString("1,2,3"));
    }

    /// <summary>
    /// 测试相关入口 MapBox_ScalesSizeByColumnNorms；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 MapBox、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MapBox_ScalesSizeByColumnNorms()
    {
        var map = new AffineTransform2D(2, 0, 10, 0, 2, 20);
        var mapped = map.MapBox(new Rect2D(10, 10, 20, 30));
        Assert.Equal(new Rect2D(30, 40, 40, 60), mapped);
    }

    /// <summary>
    /// 测试相关入口 Json_RoundTrips；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Deserialize、Serialize、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Json_RoundTrips()
    {
        var map = new AffineTransform2D(1, 0.5, 3, -0.5, 2, -7);
        var back = JsonSerializer.Deserialize<AffineTransform2D>(JsonSerializer.Serialize(map));
        Assert.Equal(map, back);
    }
}
