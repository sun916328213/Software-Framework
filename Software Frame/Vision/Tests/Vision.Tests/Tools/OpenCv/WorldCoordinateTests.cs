// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorldCoordinateTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Core.Coordinate、Vision.Core.Geometry、Vision.Tools.OpenCv.Coordinate、Vision.Tools.OpenCv.Inspection、Vision.Tools.OpenCv.Measurement。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
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
    /// <summary>
    /// 测试相关入口 TestChain；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static CoordinateChain TestChain() => new(
        new Pose2D(10, 20, 0),
        new AffineTransform2D(0.5, 0, 0, 0, 0.5, 0),
        Pose2D.Identity);

    /// <summary>
    /// 测试相关入口 BlobResult_ToWorld_MapsCenterAndBounds；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToWorld、TestChain、Equal、ToMachine；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 FindLineResult_ToMachine_MapsEndpoints；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToMachine、TestChain、Equal；具体条件和先后次序见方法体。
    /// </summary>
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
