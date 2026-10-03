// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CoordinateChainTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core.Coordinate、Vision.Core.Geometry、Xunit。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P7 测试 —— 标准坐标链 Image -> Fixture -> Calibration -> Machine。
// 期望值全部手算（注释给推导），不是金丝雀.
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Coordinate;
using Vision.Core.Geometry;
using Xunit;

namespace Vision.Tests.Core;

/// <summary>坐标链单元测试。</summary>
[Trait("Category", "Unit")]
public sealed class CoordinateChainTests
{
    /// <summary>
    /// 标准链：fixture 原点在像素 (100,50)（无旋转）；标定 world = 0.1*fixture + (5,5)；
    /// 机台 = world + (1000,2000)。像素 (100,50) → fixture (0,0) → world (5,5) →
    /// 机台 (1005,2005)。
    /// </summary>
    private static CoordinateChain StandardChain() => new(
        new Pose2D(100, 50, 0),
        new AffineTransform2D(0.1, 0, 5, 0, 0.1, 5),
        new Pose2D(1000, 2000, 0));

    /// <summary>
    /// 验证 StandardChain_MapsPixelToWorldAndMachine 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 StandardChain、Equal、PixelToWorld、PixelToMachine；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StandardChain_MapsPixelToWorldAndMachine()
    {
        var chain = StandardChain();
        Assert.Equal(new Point2D(5, 5), chain.PixelToWorld(new Point2D(100, 50)));
        Assert.Equal(new Point2D(1005, 2005), chain.PixelToMachine(new Point2D(100, 50)));
        // 另一点：像素 (200,150) → fixture (100,100) → world (15,15) → 机台 (1015,2015)。
        Assert.Equal(new Point2D(15, 15), chain.PixelToWorld(new Point2D(200, 150)));
        Assert.Equal(new Point2D(1015, 2015), chain.PixelToMachine(new Point2D(200, 150)));
    }

    /// <summary>
    /// 验证 WorldToPixel_InvertsPixelToWorld 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 StandardChain、PixelToWorld、WorldToPixel、True、DistanceTo；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WorldToPixel_InvertsPixelToWorld()
    {
        var chain = StandardChain();
        var world = chain.PixelToWorld(new Point2D(123, 456));
        var back = chain.WorldToPixel(world);
        Assert.True(back.DistanceTo(new Point2D(123, 456)) < 1e-9);
    }

    /// <summary>
    /// 验证 RotatedFixture_MapsThroughInversePose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 PixelToWorld、True、DistanceTo；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RotatedFixture_MapsThroughInversePose()
    {
        // Fixture 姿态 R90（fixture→image）：像素 (1,0) → fixture R-90(1,0) = (0,-1)。
        var chain = new CoordinateChain(
            new Pose2D(0, 0, 90),
            AffineTransform2D.Identity,
            Pose2D.Identity);
        var fixture = chain.PixelToWorld(new Point2D(1, 0));
        Assert.True(fixture.DistanceTo(new Point2D(0, -1)) < 1e-9, $"fixture={fixture}");
    }

    /// <summary>
    /// 验证 CustomLinks_MustConnect_AndRejectEmpty 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CustomLinks_MustConnect_AndRejectEmpty()
    {
        Assert.Throws<ArgumentException>(() => new CoordinateChain(
            new List<ICoordinateTransform>
            {
                new RigidTransform(CoordinateSpace.Image, CoordinateSpace.Fixture, Pose2D.Identity),
                // 故意跳过 Fixture→Calibration，直接 Calibration→Machine。
                new RigidTransform(CoordinateSpace.Calibration, CoordinateSpace.Machine, Pose2D.Identity),
            }));
        Assert.Throws<ArgumentException>(() => new CoordinateChain(
            new List<ICoordinateTransform>()));
    }

    /// <summary>
    /// 验证 ToAffine_MatchesStepwiseTransform 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 StandardChain、ToAffine、Transform、True、DistanceTo；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ToAffine_MatchesStepwiseTransform()
    {
        var chain = StandardChain();
        var affine = chain.ToAffine();
        var point = new Point2D(77, 88);
        var composed = affine.Transform(point);
        var stepwise = chain.Transform(point);
        Assert.True(composed.DistanceTo(stepwise) < 1e-9);
        Assert.Throws<ArgumentOutOfRangeException>(() => chain.ToAffine(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => chain.ToAffine(4));
    }
}
