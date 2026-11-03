#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 全图灰度滤波，保持位深/尺寸/坐标；核锚点位于中心。不隐式平滑ROI外像素后再拼接。
/// </summary>
public sealed class ImageFilterOperation : VisionImageOperation<ImageTransformOutput>
{
    public const string Id = "vision.processing.filter";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    public ImageFilterKind Kind
    {
        get;
    }
    /// <summary>正方形核边长，单位像素，奇数3..99。</summary>
    public int KernelSize
    {
        get;
    }
    public ImageBorderMode Border
    {
        get;
    }
    /// <summary>仅高斯使用，单位像素；其他方法必须为null。</summary>
    public double? GaussianSigma
    {
        get;
    }

    public ImageFilterOperation(VisionFrame image, ImageFilterKind kind, int kernelSize,
        ImageBorderMode border, double? gaussianSigma = null) : base(image)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(border))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (kernelSize < 3 || kernelSize > 99 || kernelSize % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kernelSize));
        }

        if (kind == ImageFilterKind.Gaussian)
        {
            if (gaussianSigma is not { } sigma || !double.IsFinite(sigma) || sigma < 0.01 || sigma > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(gaussianSigma), "高斯sigma必须明确给定，支持0.01..1000像素。");
            }
        }
        else if (gaussianSigma != null)
        {
            throw new ArgumentException("非高斯滤波不接受无效的sigma参数。", nameof(gaussianSigma));
        }

        Kind = kind;
        KernelSize = kernelSize;
        Border = border;
        GaussianSigma = gaussianSigma;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
