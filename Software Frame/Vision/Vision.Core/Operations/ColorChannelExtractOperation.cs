#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 从明确的Rgb24/Bgr24/Bgra32取指定R/G/B编码值，输出独立同尺寸/像素空间Mono8帧。
/// 不计算亮度、不归一化、不将alpha与背景合成；Bgra32的alpha明确忽略。
/// 全图操作，不隐式解释灰度/浮点输入，不准备模型。
/// </summary>
public sealed class ColorChannelExtractOperation : VisionImageOperation<ImageTransformOutput>
{
    public const string Id = "vision.processing.color-channel";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public RgbChannel Channel { get; }

    public ColorChannelExtractOperation(VisionFrame image, RgbChannel channel) : base(image)
    {
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        Channel = channel;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
