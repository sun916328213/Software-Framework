#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>全图按本帧最小/最大编码拉伸到同位深满量程；常量图保持原值。整数结果采用半向上舍入，不处理颜色或浮点。</summary>
public sealed class GrayNormalizeOperation(VisionFrame image) : VisionImageOperation<GrayNormalizeOutput>(image)
{
    public const string Id = "vision.processing.gray-normalize";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
