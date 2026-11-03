#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 全图整数灰度Gamma：M×(pixel/M)^Gamma，M为Mono8的255或Mono16的65535。
/// 指数直接使用Gamma而不是倒数；量化为floor(value+0.5)，不修改既有GrayLinear的SDK舍入规则。
/// 保持位深、尺寸及像素空间，生成独立帧；不按本图极值归一化，不解释颜色/浮点或修改alpha。
/// </summary>
public sealed class GrayGammaOperation : VisionImageOperation<ImageTransformOutput>
{
    public const string Id = "vision.processing.gray-gamma";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    /// <summary>明确的有限正指数；小于1提亮中间编码值，大于1压暗，不反转端点。</summary>
    public double Gamma { get; }

    public GrayGammaOperation(VisionFrame image, double gamma) : base(image)
    {
        if (!double.IsFinite(gamma) || gamma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma));
        }

        Gamma = gamma;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
