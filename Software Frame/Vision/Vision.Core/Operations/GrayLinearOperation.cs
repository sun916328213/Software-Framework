#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 明确的整数灰度线性变换：gain*pixel+offset，然后限幅到输出位深并舍入。
/// 不按当前图像最小/最大值自动拉伸，不猜测Mono16的有效灰度范围。
/// </summary>
public sealed class GrayLinearOperation : VisionImageOperation<ImageTransformOutput>
{
    public const string Id = "vision.processing.gray-linear";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    /// <summary>逐像素乘数，可为负值以进行显式反相；不由输入极值推断。</summary>
    public double Gain
    {
        get;
    }

    /// <summary>乘法后的灰度偏移，按表达式输出的灰度数值计，不按百分比解释。</summary>
    public double Offset
    {
        get;
    }

    /// <summary>明确的Mono8或Mono16输出位深，决定限幅范围。</summary>
    public PixelFormat OutputFormat
    {
        get;
    }

    /// <summary>默认使用所选后端的最近整数规则；HalfUp为显式非负半向上量化。</summary>
    public GrayLinearRounding Rounding { get; }

    public GrayLinearOperation(VisionFrame image, double gain, double offset, PixelFormat outputFormat)
        : this(image, gain, offset, outputFormat, GrayLinearRounding.BackendNearest)
    {
    }

    public GrayLinearOperation(VisionFrame image, double gain, double offset, PixelFormat outputFormat, GrayLinearRounding rounding) : base(image)
    {
        if (!Enum.IsDefined(rounding))
        {
            throw new ArgumentOutOfRangeException(nameof(rounding));
        }
        if (outputFormat is not (PixelFormat.Mono8 or PixelFormat.Mono16))
        {
            throw new ArgumentOutOfRangeException(nameof(outputFormat));
        }

        var maximum = image.Format == PixelFormat.Mono8 ? byte.MaxValue : ushort.MaxValue;
        if (!double.IsFinite(gain) || !double.IsFinite(offset) || !double.IsFinite(gain * maximum + offset))
        {
            throw new ArgumentOutOfRangeException(nameof(gain), "线性参数及输入灰度范围端点必须有限。");
        }

        Gain = gain;
        Offset = offset;
        OutputFormat = outputFormat;
        Rounding = rounding;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
