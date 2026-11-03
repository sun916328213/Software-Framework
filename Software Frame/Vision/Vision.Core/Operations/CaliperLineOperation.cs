#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Core.Operations;

/// <summary>
/// 固定或定位跟随的卡尺直线请求，不借用可编辑ILineSearchRegion或一般面积ROI。
/// 沿基准线含端点均匀布置卡尺；切向整数偏移平均，法向1像素间距采样。
/// 绑定时Start/End参数为模板局部基准；只变换名义路径，采样半长/半宽/sigma/残差限始终为当前图像像素。
/// 邻点差分峰位于两次采样的中点，不宣称工业亚像素精度，不自动换算毫米。
/// </summary>
public sealed class CaliperLineOperation : VisionImageOperation<CaliperLineOutput>
{
    public const string Id = "vision.measurement.caliper-line";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    /// <summary>绑定时为模板局部基准；未绑定时为输入像素坐标，始终不累积改写。</summary>
    public Point2D ReferenceStart
    {
        get;
    }

    public Point2D ReferenceEnd
    {
        get;
    }

    public TemplateLocationBinding? LocationBinding
    {
        get;
    }

    public Point2D Start
    {
        get;
    }

    public Point2D End
    {
        get;
    }

    public Point2D Tangent
    {
        get;
    }

    public Point2D ScanNormal
    {
        get;
    }

    public int CaliperCount
    {
        get;
    }

    public int NormalHalfLength
    {
        get;
    }

    public int TangentHalfWidth
    {
        get;
    }

    public CaliperScanDirection ScanDirection
    {
        get;
    }

    public CaliperEdgePolarity Polarity
    {
        get;
    }

    public CaliperEdgeSelection Selection
    {
        get;
    }

    public double MinimumContrast
    {
        get;
    }

    public double GaussianSigmaPixels
    {
        get;
    }

    public int MinimumPoints
    {
        get;
    }

    public double MaximumResidualPixels
    {
        get;
    }

    public CaliperLineOperation(VisionFrame image, Point2D start, Point2D end,
        int caliperCount = 16, int normalHalfLength = 10, int tangentHalfWidth = 1,
        CaliperScanDirection scanDirection = CaliperScanDirection.Forward,
        CaliperEdgePolarity polarity = CaliperEdgePolarity.Any,
        CaliperEdgeSelection selection = CaliperEdgeSelection.Strongest,
        double minimumContrast = 10, double gaussianSigmaPixels = 1,
        int minimumPoints = 3, double maximumResidualPixels = 1, TemplateLocationBinding? locationBinding = null) : base(image)
    {
        ReferenceStart = start;
        ReferenceEnd = end;
        LocationBinding = locationBinding;
        if (locationBinding != null)
        {
            var transform = locationBinding.Resolve(image).TemplateToImage;
            start = transform.Transform(start);
            end = transform.Transform(end);
        }

        var length = start.DistanceTo(end);
        if (!Finite(start) || !Finite(end) || !double.IsFinite(length) || length < 1)
        {
            throw new ArgumentException("卡尺基准线必须有限且至少长1像素。");
        }

        if (caliperCount < 2 || caliperCount > 512 || normalHalfLength < 2 || normalHalfLength > 512
            || tangentHalfWidth < 0 || tangentHalfWidth > 32 || minimumPoints < 2 || minimumPoints > caliperCount)
        {
            throw new ArgumentOutOfRangeException(nameof(caliperCount), "卡尺2..512把、法向半长2..512、切向半宽0..32，拟合至少2点。");
        }

        if (!Enum.IsDefined(scanDirection) || !Enum.IsDefined(polarity) || !Enum.IsDefined(selection))
        {
            throw new ArgumentOutOfRangeException(nameof(scanDirection));
        }

        if (!double.IsFinite(minimumContrast) || minimumContrast <= 0 || minimumContrast > ushort.MaxValue
            || !double.IsFinite(gaussianSigmaPixels) || gaussianSigmaPixels < 0 || gaussianSigmaPixels > 10
            || gaussianSigmaPixels > 0 && gaussianSigmaPixels < 0.01
            || normalHalfLength <= Math.Ceiling(3 * gaussianSigmaPixels)
            || !double.IsFinite(maximumResidualPixels) || maximumResidualPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumContrast), "阈值必须为原灰度正差值；sigma为0或0.01..10且搜索半长须超过3sigma，残差上限必须有限为正。");
        }

        if ((long)caliperCount * (2 * normalHalfLength + 1) * (2 * tangentHalfWidth + 1) > 5_000_000)
        {
            throw new ArgumentException("卡尺总采样数超过500万，请收紧宽度/长度或数量。");
        }

        var tangent = new Point2D((end.X - start.X) / length, (end.Y - start.Y) / length);
        var sign = scanDirection == CaliperScanDirection.Forward ? 1 : -1;
        var normal = new Point2D(sign * tangent.Y, -sign * tangent.X);
        foreach (var center in new[] { start, end })
        {
            foreach (var u in new[] { -tangentHalfWidth, tangentHalfWidth })
            {
                foreach (var v in new[] { -normalHalfLength, normalHalfLength })
                {
                    var x = center.X + u * tangent.X + v * normal.X;
                    var y = center.Y + u * tangent.Y + v * normal.Y;
                    if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 || x > image.Width - 1 || y > image.Height - 1)
                    {
                        throw new ArgumentException("整把卡尺必须在输入图像内，不裁切或以补边灰度制造边缘。");
                    }
                }
            }
        }

        Start = start;
        End = end;
        Tangent = tangent;
        ScanNormal = normal;
        CaliperCount = caliperCount;
        NormalHalfLength = normalHalfLength;
        TangentHalfWidth = tangentHalfWidth;
        ScanDirection = scanDirection;
        Polarity = polarity;
        Selection = selection;
        MinimumContrast = minimumContrast;
        GaussianSigmaPixels = gaussianSigmaPixels;
        MinimumPoints = minimumPoints;
        MaximumResidualPixels = maximumResidualPixels;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
        if (Image.Format == PixelFormat.Mono8 && MinimumContrast > byte.MaxValue)
        {
            issues.Add(new("ContrastRangeMismatch", "Mono8卡尺阈值不能超过255灰度差。"));
        }
    }

    private static bool Finite(Point2D point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
