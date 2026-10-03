// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FakeAIProvider、FakeAIModelSession、CannedResults。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Core.AI、Vision.Core.Geometry、Vision.Core.Results。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
// ─────────────────────────────────────────────────────────────────────────────
// P5 测试 fake —— IAIProvider / IAIModelSession 的内存实现（计划 §P5 验收：
// 「Vision.Tools.AI 不引用三家 Provider SDK；可使用 fake Provider 完整测试」）。
//
// 住在测试工程：生产代码⛔ 不得引用（SolutionStructureTests 守卫生产→测试引用）。
// 行为可编程：能力/可用性/打开失败/会话类型/推理结果全部由测试用例控制。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.AI;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Tests.Tools.AI;

/// <summary>内存 Provider（代替 HALCON / Aiditron / Invi 三家真实实现）。</summary>
internal sealed class FakeAIProvider : IAIProvider
{
    public string Name { get; set; } = "Fake";

    public AICapabilities Capabilities { get; set; } =
        AICapabilities.Classification | AICapabilities.Detection |
        AICapabilities.Segmentation | AICapabilities.AnomalyDetection;

    public bool IsAvailable { get; set; } = true;

    /// <summary>打开会话的自定义逻辑；null 表示按请求类型返回默认 fake 会话。</summary>
    public Func<AIModelRequest, IAIModelSession>? OpenSessionFunc { get; set; }

    public int OpenSessionCalls { get; private set; }

    public int CloseSessionCalls { get; private set; }

    public IAIModelSession? LastClosed { get; private set; }

    public AIModelRequest? LastRequest { get; private set; }

    /// <summary>
    /// 验证 OpenSession 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 OpenSessionFunc；具体条件和先后次序见方法体。
    /// </summary>
    public IAIModelSession OpenSession(AIModelRequest request)
    {
        OpenSessionCalls++;
        LastRequest = request;
        if (OpenSessionFunc != null)
            return OpenSessionFunc(request);
        return new FakeAIModelSession(request.ModelPath, request.ModelType, Capabilities);
    }

    /// <summary>
    /// 验证 CloseSession 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 MarkClosed；具体条件和先后次序见方法体。
    /// </summary>
    public void CloseSession(IAIModelSession session)
    {
        CloseSessionCalls++;
        LastClosed = session;
        if (session is FakeAIModelSession fake)
            fake.MarkClosed();
    }
}

/// <summary>内存会话（已加载态出生；推理返回预置结果或按 RunFunc 定制）。</summary>
internal sealed class FakeAIModelSession : IAIModelSession
{
    /// <summary>
    /// 初始化 FakeAIModelSession 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public FakeAIModelSession(string modelId, AIModelType modelType, AICapabilities capabilities)
    {
        ModelId = modelId;
        ModelType = modelType;
        Capabilities = capabilities;
    }

    public string ModelId { get; }

    public AIModelType ModelType { get; }

    public AICapabilities Capabilities { get; }

    public bool IsLoaded { get; private set; } = true;

    /// <summary>自定义推理；null 表示返回 <see cref="CannedResults"/> 默认值。</summary>
    public Func<AIInferenceRequest, ToolResult>? RunFunc { get; set; }

    public AIInferenceRequest? LastRequest { get; private set; }

    public int RunCalls { get; private set; }

    /// <summary>
    /// 验证 Run 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Default；具体条件和先后次序见方法体。
    /// </summary>
    public ToolResult Run(AIInferenceRequest request)
    {
        if (!IsLoaded)
            throw new InvalidOperationException("会话已关闭。");
        RunCalls++;
        LastRequest = request;
        return RunFunc?.Invoke(request) ?? CannedResults.Default(ModelType);
    }

    /// <summary>
    /// 验证 MarkClosed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    public void MarkClosed() => IsLoaded = false;

    /// <summary>
    /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    public void Dispose() => IsLoaded = false;
}

/// <summary>各任务类型的预置推理结果（内容固定，供断言比对）。</summary>
internal static class CannedResults
{
    /// <summary>
    /// 验证 Default 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    public static ToolResult Default(AIModelType modelType) => modelType switch
    {
        AIModelType.Classification => new AIClassificationResult
        {
            TopLabel = "OK",
            TopScore = 0.9,
            Scores =
            [
                new AIClassScore { Label = "OK", ClassId = 0, Score = 0.9 },
                new AIClassScore { Label = "NG", ClassId = 1, Score = 0.1 },
            ],
        },
        AIModelType.Detection => new AIDetectionResult
        {
            Boxes =
            [
                new AIDetectionBox { Box = new Rect2D(10, 10, 20, 20), Label = "defect", ClassId = 1, Score = 0.8 },
                new AIDetectionBox { Box = new Rect2D(50, 50, 5, 5), Label = "scratch", ClassId = 2, Score = 0.6 },
            ],
        },
        AIModelType.Segmentation => new AISegmentationResult
        {
            Items =
            [
                new AISegmentationItem { Label = "defect", Score = 0.7, Bounds = new Rect2D(0, 0, 30, 30) },
                new AISegmentationItem { Label = "dust", Score = 0.5, Bounds = new Rect2D(60, 60, 4, 4) },
            ],
        },
        AIModelType.AnomalyDetection => new AIAnomalyResult
        {
            IsAnomaly = true,
            Score = 0.66,
            AnomalyBounds = new Rect2D(5, 5, 10, 10),
        },
        _ => new AIClassificationResult(),
    };
}
