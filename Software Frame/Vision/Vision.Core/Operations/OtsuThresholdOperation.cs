#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 全图Otsu阈值；普通模式pixel>阈值时为255，反向模式pixel≤阈值时为255。
/// 统计输入原始位深，输出Mono8二值图和本次实际阈值；不隐式先转8位。
/// </summary>
public sealed class OtsuThresholdOperation(VisionFrame image, bool inverse = false)
    : VisionImageOperation<OtsuThresholdOutput>(image)
{
    public const string Id = "vision.processing.threshold-otsu";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public bool Inverse { get; } = inverse;

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
