#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 显式像素格式转换，不改变尺寸或坐标。具体格式组合由后端声明，不自动归一化或选择灰度范围。
/// 灰度范围重映射应使用GrayLinearOperation，不能把Mono16到Mono8当成无损转换。
/// </summary>
public sealed class ImageConvertOperation : VisionImageOperation<ImageTransformOutput>
{
    public const string Id = "vision.processing.convert";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    /// <summary>明确要求的输出格式，不由显示控件或SDK默认值推断。</summary>
    public PixelFormat OutputFormat
    {
        get;
    }

    public ImageConvertOperation(VisionFrame image, PixelFormat outputFormat) : base(image)
    {
        if (!Enum.IsDefined(outputFormat) || outputFormat == PixelFormat.Unknown || outputFormat == image.Format)
        {
            throw new ArgumentOutOfRangeException(nameof(outputFormat), "必须指定有效且不同于输入的像素格式。");
        }

        OutputFormat = outputFormat;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
