#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 全图0/255二值形态学，不将灰度图自动阈值化。核锚点位于中心，边界规则必须明确。
/// 开/闭的Iterations表示各基础腐蚀/膨胀阶段次数，而不是重复整个开/闭运算。
/// </summary>
public sealed class MorphologyOperation : VisionImageOperation<BinaryImageOutput>
{
    public const string Id = "vision.processing.morphology";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    public MorphologyKind Kind
    {
        get;
    }
    public MorphologyKernelShape KernelShape
    {
        get;
    }
    public int KernelWidth
    {
        get;
    }
    public int KernelHeight
    {
        get;
    }
    public ImageBorderMode Border
    {
        get;
    }
    public int Iterations
    {
        get;
    }

    public MorphologyOperation(VisionFrame image, MorphologyKind kind, MorphologyKernelShape kernelShape,
        int kernelWidth, int kernelHeight, ImageBorderMode border, int iterations = 1) : base(image)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(kernelShape) || !Enum.IsDefined(border))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (kernelWidth < 1 || kernelWidth > 99 || kernelWidth % 2 == 0
            || kernelHeight < 1 || kernelHeight > 99 || kernelHeight % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kernelWidth), "形态学核宽高必须为1..99的奇数。");
        }

        if (iterations < 1 || iterations > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations));
        }

        if (image.Format == PixelFormat.Mono8)
        {
            for (var row = 0; row < image.Height; row++)
            {
                foreach (var pixel in image.Pixels.Slice(row * image.Stride, image.Width))
                {
                    if (pixel is not (0 or 255))
                    {
                        throw new ArgumentException("二值形态学只接受0/255图像，请先明确执行阈值操作。", nameof(image));
                    }
                }
            }
        }

        Kind = kind;
        KernelShape = kernelShape;
        KernelWidth = kernelWidth;
        KernelHeight = kernelHeight;
        Border = border;
        Iterations = iterations;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
