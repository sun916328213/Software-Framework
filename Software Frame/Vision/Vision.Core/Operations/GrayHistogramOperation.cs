#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Core.Operations;

/// <summary>
/// 原编码整数灰度直方图及原位深中位数；不归一化、转灰或改变既有灰度统计请求。
/// 第i箱包含[i×BinWidth, min((i+1)×BinWidth-1,最大编码值)]，最后一箱可不足宽度。
/// 中位数在分箱前按完整灰度排序计算，偶数像素取中间两值均值，不取箱中心。
/// </summary>
public sealed class GrayHistogramOperation : VisionImageOperation<GrayHistogramOutput>
{
    public const string Id = "vision.measurement.gray-histogram";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => Region == null
        ? VisionOperationFeatures.None : VisionOperationFeatures.ProcessingRegion;

    /// <summary>正整数灰度编码宽度；Mono8支持1..256，Mono16支持1..65536。</summary>
    public int BinWidth { get; }

    /// <summary>冻结的输入像素坐标参与域；null为全图，域外像素不计入任意箱。</summary>
    public SearchRegion? Region { get; }

    public GrayHistogramOperation(VisionFrame image, int binWidth = 1, SearchRegion? region = null) : base(image)
    {
        if (binWidth is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(binWidth));
        }

        if (region != null && !region.Runs(image.Width, image.Height).Any())
        {
            throw new ArgumentException("直方图参与域与输入图像没有有效像素。", nameof(region));
        }

        BinWidth = binWidth;
        Region = region;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
        if (Image.Format == PixelFormat.Mono8 && BinWidth > 256)
        {
            issues.Add(new("HistogramBinWidthMismatch", "Mono8直方图箱宽只能为1..256，不截断参数或隐式升位深。"));
        }
    }
}
