// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorldCoordinateTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Core.Coordinate、Vision.Core.Geometry、Vision.Tools.Halcon.Coordinate、Vision.Tools.Halcon.Inspection、Vision.Tools.Halcon.Measurement。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
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
    /// <summary>
    /// 测试相关入口 TestChain；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildPose；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 BlobResult_ToWorld_AndToMachine；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToWorld、TestChain、True、Equal、ToMachine；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 FindLineResult_ToWorld_MapsEndpoints；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToWorld、TestChain、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 NonImageChain_IsRejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、ToWorld；具体条件和先后次序见方法体。
    /// </summary>
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
