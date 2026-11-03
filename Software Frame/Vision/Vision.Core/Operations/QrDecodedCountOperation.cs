#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>只判本次多QR返回候选中的成功解码数量，不把仅检测框算成功，也不证明全场景无漏检。</summary>
public sealed class QrDecodedCountOperation : VisionOperation<QrCountJudgmentOutput>
{
    public const string Id = "vision.evaluation.qr-decoded-count";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public QrCodeReadManyOutput Source
    {
        get;
    }

    public int Minimum
    {
        get;
    }

    public int Maximum
    {
        get;
    }

    public QrDecodedCountOperation(QrCodeReadManyOutput source, int minimum, int maximum)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (minimum < 0 || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "解码计数范围必须非负有序，按闭区间判定。");
        }
        Source = source;
        Minimum = minimum;
        Maximum = maximum;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
