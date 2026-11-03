#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>对已完成单目标QR快照作Ordinal大小写敏感全文相等判定，无图无SDK；未解码为NG而非成功读码。</summary>
public sealed class QrTextEqualsOperation : VisionOperation<QrTextJudgmentOutput>
{
    public const string Id = "vision.evaluation.qr-text-equals";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public QrCodeReadOutput Source
    {
        get;
    }

    public string ExpectedText
    {
        get;
    }

    public QrTextEqualsOperation(QrCodeReadOutput source, string expectedText)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(expectedText) || expectedText.Length > 8192)
        {
            throw new ArgumentException("期望QR文本必须非空且不超过8192字符，不截断或隐式规范化。", nameof(expectedText));
        }
        Source = source;
        ExpectedText = expectedText;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
