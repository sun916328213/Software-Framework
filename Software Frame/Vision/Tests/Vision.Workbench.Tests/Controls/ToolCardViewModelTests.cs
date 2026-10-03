// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ToolCardViewModelTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Tests.Tools.AI、Vision.Workbench、Vision.Workbench.Tests、Vision.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
using Vision.Tests.Tools.AI;
using Vision.Workbench;
using Vision.Workbench.Tests;
// ─────────────────────────────────────────────────────────────────────────────
// P8 测试 —— ToolCardViewModel（计划 §P8「关键 ViewModel 逻辑测试」）。
// 全程无 UI 线程：OpenCV 真实执行（合成图）、HALCON/AI 走无 Runtime 路径.
// 原先住在 Vision.Tests；按分层规则「UI 层测试独占 WPF」迁到本工程，
// 合成图工厂与 fake Provider 由 csproj 链接编译复用（见工程文件注释）。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Controls.ViewModels;



namespace Vision.Workbench.Tests.Controls;

/// <summary>Tool 卡片 VM 测试。</summary>
[Trait("Category", "Unit")]
public sealed class ToolCardViewModelTests
{
    /// <summary>
    /// 验证 OpenCvThreshold_RefreshRun_ShowsRealResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Execute、True、SetImage、WhiteRectangle；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenCvThreshold_RefreshRun_ShowsRealResult()
    {
        var tool = new Vision.Tools.OpenCv.Processing.ThresholdTool();
        using var adapter = new OpenCvToolCardAdapter<
            Vision.Tools.OpenCv.Processing.ThresholdParameters,
            Vision.Tools.OpenCv.Processing.ThresholdResult>(tool);
        var card = new ToolCardViewModel(adapter);
        Assert.Equal("Threshold", card.ToolName);

        card.RefreshValidationCommand.Execute(null);
        Assert.True(card.IsValid);
        Assert.Equal("校验通过", card.StatusText);

        card.SetImage(SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60));
        tool.Parameters.MinGray = 128;
        card.RunCommand.Execute(null);
        Assert.Equal("执行成功", card.StatusText);
        Assert.True(card.ElapsedMs >= 0);
        Assert.Contains("600", card.ResultSummary);
    }

    /// <summary>
    /// 验证 HalconBlob_InvalidParams_ShowsValidationError_WithoutRuntime 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Execute、False、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HalconBlob_InvalidParams_ShowsValidationError_WithoutRuntime()
    {
        var tool = new Vision.Tools.Halcon.Inspection.BlobTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.MaxGray = 100;
        using var adapter = new HalconToolCardAdapter<
            Vision.Tools.Halcon.Inspection.BlobParameters,
            Vision.Tools.Halcon.Inspection.BlobResult>(tool);
        var card = new ToolCardViewModel(adapter);

        card.RefreshValidationCommand.Execute(null);
        Assert.False(card.IsValid);
        Assert.StartsWith("校验失败：", card.StatusText);
    }

    /// <summary>
    /// 验证 AIDetection_WithoutProvider_LoadFailsIntoStatus 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Execute、False、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AIDetection_WithoutProvider_LoadFailsIntoStatus()
    {
        var tool = new Vision.Tools.AI.Detection.DetectionTool();
        tool.Parameters.ModelPath = "det.model";
        using var adapter = new AIToolCardAdapter<
            Vision.Tools.AI.Detection.DetectionParameters,
            Vision.Core.AI.AIDetectionResult>(tool);
        var card = new ToolCardViewModel(adapter);

        card.EnsureLoadedCommand.Execute(null);
        Assert.False(card.IsValid);
        Assert.StartsWith("加载失败：", card.StatusText);
    }

    /// <summary>
    /// 验证 AIDetection_WithFakeProvider_RunsEndToEnd 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Execute、Equal、SetImage、WhiteRectangle、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AIDetection_WithFakeProvider_RunsEndToEnd()
    {
        var tool = new Vision.Tools.AI.Detection.DetectionTool();
        tool.Parameters.ModelPath = "det.model";
        using var adapter = new AIToolCardAdapter<
            Vision.Tools.AI.Detection.DetectionParameters,
            Vision.Core.AI.AIDetectionResult>(tool);
        adapter.Provider = new FakeAIProvider();
        var card = new ToolCardViewModel(adapter);

        card.EnsureLoadedCommand.Execute(null);
        Assert.Equal("就绪", card.StatusText);
        card.SetImage(SyntheticImages.WhiteRectangle(64, 64, 10, 10, 30, 30));
        card.RunCommand.Execute(null);
        Assert.Equal("执行成功", card.StatusText);
        Assert.Contains("检测：", card.ResultSummary);
    }

    /// <summary>
    /// 验证 PropertyChanged_Fires_OnStatusUpdate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Execute、Contains、nameof；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PropertyChanged_Fires_OnStatusUpdate()
    {
        var tool = new Vision.Tools.OpenCv.Processing.ThresholdTool();
        using var adapter = new OpenCvToolCardAdapter<
            Vision.Tools.OpenCv.Processing.ThresholdParameters,
            Vision.Tools.OpenCv.Processing.ThresholdResult>(tool);
        var card = new ToolCardViewModel(adapter);

        var raised = new List<string>();
        card.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "?");
        card.RefreshValidationCommand.Execute(null);
        Assert.Contains(nameof(ToolCardViewModel.StatusText), raised);
        Assert.Contains(nameof(ToolCardViewModel.IsValid), raised);
    }
}