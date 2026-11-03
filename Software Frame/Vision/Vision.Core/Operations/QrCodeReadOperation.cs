#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;
using Vision.Core.Imaging;

namespace Vision.Core.Operations;

/// <summary>
/// 单目标标准QR文本读取，Mono8、整数半开矩形搜索窗；未指定宽高时取x/y到右下边界的剩余区域。
/// 多码场景只可能读取原生选中的一个，不承诺全码计数/排序；不裁造背景或隐式转灰。
/// </summary>
public sealed class QrCodeReadOperation : VisionImageOperation<QrCodeReadOutput>
{
    public const string Id = "vision.identification.single-qr";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
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

    public QrCodeReadOperation(VisionFrame image, int x = 0, int y = 0, int? width = null, int? height = null) : base(image)
    {
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "QR搜索窗起点必须在原图内。");
        }
        var w = width ?? image.Width - x;
        var h = height ?? image.Height - y;
        if (w < 21 || h < 21 || (long)x + w > image.Width || (long)y + h > image.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "标准QR搜索窗每边至少21像素，且不得越界。");
        }
        X = x;
        Y = y;
        Width = w;
        Height = h;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
