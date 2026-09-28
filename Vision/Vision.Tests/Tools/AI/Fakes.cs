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

    public IAIModelSession OpenSession(AIModelRequest request)
    {
        OpenSessionCalls++;
        LastRequest = request;
        if (OpenSessionFunc != null)
            return OpenSessionFunc(request);
        return new FakeAIModelSession(request.ModelPath, request.ModelType, Capabilities);
    }

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

    public ToolResult Run(AIInferenceRequest request)
    {
        if (!IsLoaded)
            throw new InvalidOperationException("会话已关闭。");
        RunCalls++;
        LastRequest = request;
        return RunFunc?.Invoke(request) ?? CannedResults.Default(ModelType);
    }

    public void MarkClosed() => IsLoaded = false;

    public void Dispose() => IsLoaded = false;
}

/// <summary>各任务类型的预置推理结果（内容固定，供断言比对）。</summary>
internal static class CannedResults
{
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
