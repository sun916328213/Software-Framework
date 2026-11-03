#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;
using Vision.Core.Imaging;

namespace Vision.Core.Operations;

/// <summary>
/// 标准QR多候选检测与逐候选解码；MaximumCandidates是原生返回候选预算，超限失败，不截断假计数。
/// 计数只针对本次检测返回集，不证明物理场景所有码都已被发现。
/// </summary>
public sealed class QrCodeReadManyOperation : VisionImageOperation<QrCodeReadManyOutput>
{
    public const string Id = "vision.identification.multiple-qr";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.MultipleMatches;
    public int X
    {
        get;
    }

    public int Y
    {
        get;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public int MaximumCandidates
    {
        get;
    }

    public QrCodeReadManyOperation(VisionFrame image, int maximumCandidates = 8,
        int x = 0, int y = 0, int? width = null, int? height = null) : base(image)
    {
        if (maximumCandidates < 1 || maximumCandidates > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCandidates), "多QR候选预算须为1..32，不以截断掩盖超限。");
        }
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }
        var w = width ?? image.Width - x;
        var h = height ?? image.Height - y;
        if (w < 21 || h < 21 || (long)x + w > image.Width || (long)y + h > image.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "多QR矩形窗须图内且每边至少21像素。");
        }
        X = x;
        Y = y;
        Width = w;
        Height = h;
        MaximumCandidates = maximumCandidates;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
