// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AIRoiTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Core.AI、Vision.Core.Geometry、Vision.Core.Imaging、Vision.Core.Roi、Vision.Tools.AI.AnomalyDetection。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
using Vision.Core.AI;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Roi;
using Vision.Tools.AI.AnomalyDetection;
using Vision.Tools.AI.Classification;
using Vision.Tools.AI.Detection;
using Vision.Tools.AI.Segmentation;
using Xunit;

namespace Vision.Tests.Tools.AI;

/// <summary>
/// AIRoiTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Trait("Category", "Contract")]
public sealed class AIRoiTests
{
    /// <summary>
    /// 验证 Image 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、ToArray、Select、Range；具体条件和先后次序见方法体。
    /// </summary>
    private static VisionImage Image() =>
        VisionImage.FromBuffer(6, 5, PixelFormat.Mono8, Enumerable.Range(0, 30).Select(x => (byte)x).ToArray(), source: "roi-test");

    /// <summary>
    /// 验证 Rectangle 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Rectangle；具体条件和先后次序见方法体。
    /// </summary>
    private static Roi Rectangle() => Roi.Rectangle(3, 2, 2, 2); // 原图 [2,4) × [1,3)

    /// <summary>
    /// 验证 Detection_CropsInputAndRestoresBoxToOriginalImageCoordinates 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Rectangle、LoadModel、Image、Run、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Detection_CropsInputAndRestoresBoxToOriginalImageCoordinates()
    {
        FakeAIModelSession? session = null;
        var provider = new FakeAIProvider
        {
            OpenSessionFunc = request =>
            {
                session = new FakeAIModelSession(request.ModelPath, request.ModelType, AICapabilities.Detection)
                {
                    RunFunc = _ => new AIDetectionResult
                    {
                        Boxes = [new AIDetectionBox { Box = new Rect2D(0, 0, 1, 1) }],
                    },
                };
                return session;
            },
        };
        using var tool = new DetectionTool { Provider = provider, Roi = Rectangle() };
        tool.Parameters.ModelPath = "det.model";
        tool.LoadModel();

        var original = Image();
        original.FrameId = 42;
        var result = tool.Run(original);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new Rect2D(2, 1, 1, 1), Assert.Single(result.Boxes).Box);
        var request = Assert.IsType<AIInferenceRequest>(session!.LastRequest);
        Assert.Null(request.Roi);
        Assert.Equal(2, request.Image.Width);
        Assert.Equal(2, request.Image.Height);
        Assert.Equal(new byte[] { 8, 9, 14, 15 }, request.Image.Pixels);
        Assert.Equal(42, request.Image.FrameId);
        Assert.Equal(original.Timestamp, request.Image.Timestamp);
        Assert.Equal("roi-test", request.Image.Source);
    }

    /// <summary>
    /// 验证 Classification_CropsPaddedInput 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Rectangle、LoadModel、FromBuffer、Run、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Classification_CropsPaddedInput()
    {
        FakeAIModelSession? session = null;
        var provider = new FakeAIProvider
        {
            OpenSessionFunc = request =>
            {
                session = new FakeAIModelSession(request.ModelPath, request.ModelType, AICapabilities.Classification);
                return session;
            },
        };
        using var tool = new ClassificationTool { Provider = provider, Roi = Roi.Rectangle(2, 1, 2, 2) };
        tool.Parameters.ModelPath = "cls.model";
        tool.LoadModel();
        var image = VisionImage.FromBuffer(3, 2, PixelFormat.Mono8, [1, 2, 3, 99, 4, 5, 6, 99], stride: 4);

        var result = tool.Run(image);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new byte[] { 2, 3, 5, 6 }, session!.LastRequest!.Image.Pixels);
    }

    /// <summary>
    /// 验证 Segmentation_RestoresBoundsAndPolygon 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Rectangle、LoadModel、Run、Image、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Segmentation_RestoresBoundsAndPolygon()
    {
        var provider = new FakeAIProvider
        {
            OpenSessionFunc = request => new FakeAIModelSession(request.ModelPath, request.ModelType, AICapabilities.Segmentation)
            {
                RunFunc = _ => new AISegmentationResult
                {
                    Items = [new AISegmentationItem
                    {
                        Bounds = new Rect2D(0, 0, 2, 2),
                        Polygon = [new Point2D(0, 0), new Point2D(1, 1)],
                    }],
                },
            },
        };
        using var tool = new SegmentationTool { Provider = provider, Roi = Rectangle() };
        tool.Parameters.ModelPath = "seg.model";
        tool.LoadModel();

        var result = tool.Run(Image());

        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Items);
        Assert.Equal(new Rect2D(2, 1, 2, 2), item.Bounds);
        Assert.Equal(new Point2D(2, 1), item.Polygon[0]);
        Assert.Equal(new Point2D(3, 2), item.Polygon[1]);
    }

    /// <summary>
    /// 验证 AnomalyDetection_RestoresLocationButKeepsEmptyLocation 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Rectangle、LoadModel、Run、Image、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AnomalyDetection_RestoresLocationButKeepsEmptyLocation()
    {
        var provider = new FakeAIProvider
        {
            OpenSessionFunc = request => new FakeAIModelSession(request.ModelPath, request.ModelType, AICapabilities.AnomalyDetection)
            {
                RunFunc = _ => new AIAnomalyResult { IsAnomaly = true, AnomalyBounds = new Rect2D(0, 0, 1, 1) },
            },
        };
        using var tool = new AnomalyDetectionTool { Provider = provider, Roi = Rectangle() };
        tool.Parameters.ModelPath = "anomaly.model";
        tool.LoadModel();

        var result = tool.Run(Image());

        Assert.True(result.Success, result.Message);
        Assert.Equal(new Rect2D(2, 1, 1, 1), result.AnomalyBounds);
    }

    /// <summary>
    /// 验证 UnsupportedShapeAndOutOfBoundsRoi_FailBeforeProviderRun 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Circle、LoadModel、Run、Image、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UnsupportedShapeAndOutOfBoundsRoi_FailBeforeProviderRun()
    {
        FakeAIModelSession? session = null;
        var provider = new FakeAIProvider
        {
            OpenSessionFunc = request =>
            {
                session = new FakeAIModelSession(request.ModelPath, request.ModelType, AICapabilities.Detection);
                return session;
            },
        };
        using var tool = new DetectionTool { Provider = provider, Roi = Roi.Circle(2, 2, 2) };
        tool.Parameters.ModelPath = "det.model";
        tool.LoadModel();

        var shapeResult = tool.Run(Image());
        Assert.False(shapeResult.Success);
        Assert.Contains("仅支持轴对齐矩形", shapeResult.Message);

        tool.Roi = Roi.Rectangle(6, 2, 2, 2);
        var boundsResult = tool.Run(Image());
        Assert.False(boundsResult.Success);
        Assert.Contains("落在图像内", boundsResult.Message);

        tool.Roi = Roi.Rectangle(3, 2, -2, 2);
        var emptyResult = tool.Run(Image());
        Assert.False(emptyResult.Success);
        Assert.Contains("非空矩形", emptyResult.Message);

        tool.Roi = Roi.Rectangle(2.5, 2, 2, 2);
        var fractionalResult = tool.Run(Image());
        Assert.False(fractionalResult.Success);
        Assert.Contains("整数像素", fractionalResult.Message);
        Assert.Equal(0, session!.RunCalls);
    }

    /// <summary>
    /// 验证 SegmentationMask_ReturnsFailureInsteadOfMisplacingPixels 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、Rectangle、LoadModel、Run、Image；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SegmentationMask_ReturnsFailureInsteadOfMisplacingPixels()
    {
        var provider = new FakeAIProvider
        {
            OpenSessionFunc = request => new FakeAIModelSession(request.ModelPath, request.ModelType, AICapabilities.Segmentation)
            {
                RunFunc = _ => new AISegmentationResult
                {
                    Items = [new AISegmentationItem
                    {
                        Bounds = new Rect2D(0, 0, 2, 2),
                        Mask = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 0, 0, 1]),
                    }],
                },
            },
        };
        using var tool = new SegmentationTool { Provider = provider, Roi = Rectangle() };
        tool.Parameters.ModelPath = "seg.model";
        tool.LoadModel();

        var result = tool.Run(Image());

        Assert.False(result.Success);
        Assert.Contains("位图掩膜", result.Message);
    }
}
