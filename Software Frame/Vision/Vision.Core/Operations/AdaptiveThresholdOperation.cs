#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 全图局部阈值，T=邻域均值或高斯加权均值-Offset，普通模式pixel>T时为255。
/// 边界复制最近像素，核锚点位于中心；邻域统计按整数图像实现舍入。
/// 不自动缩放灰度，不使用单一全局阈值代替。
/// </summary>
public sealed class AdaptiveThresholdOperation : VisionImageOperation<BinaryImageOutput>
{
    public const string Id = "vision.processing.threshold-adaptive";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    public AdaptiveThresholdMethod Method
    {
        get;
    }
    /// <summary>局部窗口边长，单位像素，奇数3..255。</summary>
    public int BlockSize
    {
        get;
    }
    /// <summary>从邻域统计值减去的整数偏移，原始灰度单位，支持-255..255。</summary>
    public int Offset
    {
        get;
    }
    public bool Inverse
    {
        get;
    }

    public AdaptiveThresholdOperation(VisionFrame image, AdaptiveThresholdMethod method,
        int blockSize, int offset, bool inverse = false) : base(image)
    {
        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }

        if (blockSize < 3 || blockSize > 255 || blockSize % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize));
        }

        if (offset < -255 || offset > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        Method = method;
        BlockSize = blockSize;
        Offset = offset;
        Inverse = inverse;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
