// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HalconProviderTests、HalconAIModelFactAttribute、HalconDetectionModelFactAttribute。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Tests.Tools.AI、OpenCvSharp、Vision.AI.Halcon、Vision.Core.AI、Vision.Core.Geometry。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
using Vision.Tests.Tools.AI;
// ─────────────────────────────────────────────────────────────────────────────
// P6 验收测试 —— HALCON AI Provider。
// 无 Runtime 环境：构造/声明/类型映射/失败回滚可测；OpenSession 走真实
// ReadDlModel，缺 Runtime 或缺文件时断言“带路径上下文的失败”；映射器纯测全覆盖。
// 真实模型推理留 RequiresRuntime。
// ─────────────────────────────────────────────────────────────────────────────

using OpenCvSharp;
using Vision.AI.Halcon;
using Vision.Core.AI;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Roi;
using Vision.Tools.AI.Classification;
using Vision.Tools.AI.Detection;
using Vision.Tools.OpenCv.Internal;
using Xunit;

namespace Vision.Tests.Tools.AI.Halcon;

/// <summary>HALCON AI Provider 声明、无 Runtime 行为与映射测试。</summary>
[Trait("Category", "Contract")]
public sealed class HalconProviderTests
{
    /// <summary>
    /// 测试相关入口 Provider_DeclaresNameAndAllFourCapabilities；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、True、HasFlag；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Provider_DeclaresNameAndAllFourCapabilities()
    {
        var provider = new HalconAIProvider();
        Assert.Equal("Halcon", provider.Name);
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Classification));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Detection));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Segmentation));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.AnomalyDetection));
    }

    /// <summary>
    /// 测试相关入口 Provider_IsAvailable_DoesNotThrow；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    [Fact]
    public void Provider_IsAvailable_DoesNotThrow()
    {
        var provider = new HalconAIProvider();
        _ = provider.IsAvailable; // 有/无 Runtime 都是合法值，关键是不抛。
    }

    /// <summary>
    /// 测试相关入口 OpenSession_UnknownType_ThrowsNotSupported；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、OpenSession；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenSession_UnknownType_ThrowsNotSupported()
    {
        var provider = new HalconAIProvider();
        Assert.Throws<NotSupportedException>(() =>
            provider.OpenSession(new AIModelRequest { ModelPath = "m.hdl", ModelType = AIModelType.Unknown }));
    }

    /// <summary>
    /// 测试相关入口 OpenSession_MissingFile_ThrowsWithPathContext；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、OpenSession、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenSession_MissingFile_ThrowsWithPathContext()
    {
        var provider = new HalconAIProvider();
        // 缺 Runtime 时 ReadDlModel 走 DllNotFound；有 Runtime 时走文件不存在——
        // 两种都必须包模型路径上下文后上抛，且不留下半个会话（抛异常即无会话）。
        var ex = Assert.Throws<InvalidOperationException>(() =>
            provider.OpenSession(new AIModelRequest { ModelPath = "no-such-model.hdl", ModelType = AIModelType.Classification }));
        Assert.Contains("no-such-model.hdl", ex.Message);
    }

    /// <summary>
    /// 测试相关入口 CloseSession_Null_IsNoOp；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CloseSession；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CloseSession_Null_IsNoOp()
    {
        var provider = new HalconAIProvider();
        provider.CloseSession(null);
    }

    /// <summary>
    /// 测试相关入口 Mapper_MapsClassification_PickingTopScore；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 MapClassification、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mapper_MapsClassification_PickingTopScore()
    {
        var result = HalconResultMapper.MapClassification(
            [0, 1], [0.2, 0.85], ["ok", "ng"]);
        Assert.Equal("ng", result.TopLabel);
        Assert.Equal(0.85, result.TopScore);
        Assert.Equal(2, result.Scores.Count);
        Assert.Equal(1, result.Scores[1].ClassId);
    }

    /// <summary>
    /// 测试相关入口 Mapper_MapsClassification_WithoutNames_FallsBackToIds；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 MapClassification、Equal、Empty；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mapper_MapsClassification_WithoutNames_FallsBackToIds()
    {
        var result = HalconResultMapper.MapClassification([3], [0.5]);
        Assert.Equal("class_3", result.TopLabel);
        Assert.Empty(HalconResultMapper.MapClassification([], []).Scores);
    }

    /// <summary>
    /// 测试相关入口 Mapper_MapsDetection_WithPixelBoxes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 MapDetection、Single、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mapper_MapsDetection_WithPixelBoxes()
    {
        var result = HalconResultMapper.MapDetection(
            [10], [20], [29], [49], [2], [0.77], ["a", "b", "c"]);
        var box = Assert.Single(result.Boxes);
        Assert.Equal("c", box.Label);
        Assert.Equal(2, box.ClassId);
        Assert.Equal(0.77, box.Score);
        Assert.Equal(new Rect2D(20, 10, 30, 20), box.Box);
    }

    /// <summary>
    /// 测试相关入口 Mapper_MapsSegmentation_AndAnomaly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 MapSegmentation、Single、Equal、Empty、MapAnomaly；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mapper_MapsSegmentation_AndAnomaly()
    {
        var segmentation = HalconResultMapper.MapSegmentation(
            [(new Rect2D(0, 0, 10, 10), 1, 0.6)]);
        var item = Assert.Single(segmentation.Items);
        Assert.Equal("class_1", item.Label);
        Assert.Empty(item.Polygon); // P6-v1 只有外接框。

        var anomaly = HalconResultMapper.MapAnomaly(0.9, true, new Rect2D(5, 5, 8, 8));
        Assert.True(anomaly.IsAnomaly);
        Assert.Equal(0.9, anomaly.Score);
        Assert.Equal(new Rect2D(5, 5, 8, 8), anomaly.AnomalyBounds);
    }

    /// <summary>
    /// 测试相关入口 Classification_WithRoi_RunsAgainstRealModel；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Rectangle、GetEnvironmentVariable、LoadModel、FromBuffer、Run；具体条件和先后次序见方法体。
    /// </summary>
    [HalconAIModelFact]
    [Trait("Category", "RequiresRuntime")]
    public void Classification_WithRoi_RunsAgainstRealModel()
    {
        using var tool = new ClassificationTool
        {
            Provider = new HalconAIProvider(),
            Roi = Roi.Rectangle(32, 32, 32, 32),
        };
        tool.Parameters.ModelPath = Environment.GetEnvironmentVariable("VISION_HALCON_CLASSIFICATION_MODEL")!;
        tool.Parameters.DeviceId = "cpu";
        tool.LoadModel();
        var image = VisionImage.FromBuffer(64, 64, PixelFormat.Mono8, new byte[64 * 64]);

        var result = tool.Run(image);

        Assert.True(result.Success, result.Message);
        Assert.NotEmpty(result.Scores);
        Assert.True(double.IsFinite(result.TopScore));
    }

    /// <summary>
    /// 测试相关入口 Detection_WithRoi_ReturnsBoxesInOriginalImageCoordinates；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetEnvironmentVariable、ImRead、ToVisionImage、Rectangle、LoadModel；具体条件和先后次序见方法体。
    /// </summary>
    [HalconDetectionModelFact]
    [Trait("Category", "RequiresRuntime")]
    public void Detection_WithRoi_ReturnsBoxesInOriginalImageCoordinates()
    {
        var imagePath = Environment.GetEnvironmentVariable("VISION_HALCON_DETECTION_IMAGE")!;
        using var mat = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
        var image = MatConverter.ToVisionImage(mat, imagePath);
        var left = image.Width / 4;
        var top = image.Height / 4;
        var width = image.Width / 2;
        var height = image.Height / 2;
        using var tool = new DetectionTool
        {
            Provider = new HalconAIProvider(),
            Roi = Roi.Rectangle(left + width / 2.0, top + height / 2.0, width, height),
        };
        tool.Parameters.ModelPath = Environment.GetEnvironmentVariable("VISION_HALCON_DETECTION_MODEL")!;
        tool.Parameters.DeviceId = "cpu";
        tool.LoadModel();

        var result = tool.Run(image);

        Assert.True(result.Success, result.Message);
        Assert.NotEmpty(result.Boxes);
        Assert.All(result.Boxes, box =>
        {
            Assert.InRange(box.Box.X, left, left + width);
            Assert.InRange(box.Box.Y, top, top + height);
            Assert.True(box.Box.X + box.Box.Width <= left + width);
            Assert.True(box.Box.Y + box.Box.Height <= top + height);
        });
    }
}

/// <summary>
/// HalconAIModelFactAttribute 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：FactAttribute。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class HalconAIModelFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化 HalconAIModelFactAttribute 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 GetEnvironmentVariable、IsNullOrWhiteSpace、Exists；具体条件和先后次序见方法体。
    /// </summary>
    public HalconAIModelFactAttribute()
    {
        var path = Environment.GetEnvironmentVariable("VISION_HALCON_CLASSIFICATION_MODEL");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Skip = "设置 VISION_HALCON_CLASSIFICATION_MODEL 为可用的 HALCON 分类 .hdl 模型路径后运行。";
    }
}

/// <summary>
/// HalconDetectionModelFactAttribute 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：FactAttribute。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class HalconDetectionModelFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化 HalconDetectionModelFactAttribute 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 GetEnvironmentVariable、IsNullOrWhiteSpace、Exists；具体条件和先后次序见方法体。
    /// </summary>
    public HalconDetectionModelFactAttribute()
    {
        var model = Environment.GetEnvironmentVariable("VISION_HALCON_DETECTION_MODEL");
        var image = Environment.GetEnvironmentVariable("VISION_HALCON_DETECTION_IMAGE");
        if (string.IsNullOrWhiteSpace(model) || !File.Exists(model) ||
            string.IsNullOrWhiteSpace(image) || !File.Exists(image))
            Skip = "设置 VISION_HALCON_DETECTION_MODEL 和 VISION_HALCON_DETECTION_IMAGE 后运行。";
    }
}
