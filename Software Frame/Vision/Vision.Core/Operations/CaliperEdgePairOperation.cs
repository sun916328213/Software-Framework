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
/// 同一法向剖面内相邻合格异极性峰的条带宽度，不是两条独立拟合直线的距离。
/// 宽度为扫描轴上两峰中点的间距（像素），不自动补偿条带倾斜、不换算毫米。
/// 绑定时Start/End为模板局部基准，只变换路径；采样尺寸、sigma、宽度上下限仍是当前图像像素。
/// </summary>
public sealed class CaliperEdgePairOperation : VisionImageOperation<CaliperEdgePairOutput>
{
    public const string Id = "vision.measurement.caliper-edge-pair";
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

    public CaliperPairPolarity Polarity
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

    public double MinimumWidthPixels
    {
        get;
    }

    public double MaximumWidthPixels
    {
        get;
    }

    public int MinimumPairs
    {
        get;
    }

    public CaliperEdgePairOperation(VisionFrame image, Point2D start, Point2D end,
        double minimumWidthPixels, double maximumWidthPixels,
        CaliperPairPolarity polarity = CaliperPairPolarity.LightStripe,
        CaliperEdgeSelection selection = CaliperEdgeSelection.Strongest,
        int caliperCount = 16, int normalHalfLength = 10, int tangentHalfWidth = 1,
        CaliperScanDirection scanDirection = CaliperScanDirection.Forward,
        double minimumContrast = 10, double gaussianSigmaPixels = 1, int minimumPairs = 1, TemplateLocationBinding? locationBinding = null) : base(image)
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

        var length = double.Hypot(end.X - start.X, end.Y - start.Y);
        if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) || !double.IsFinite(end.X)
            || !double.IsFinite(end.Y) || !double.IsFinite(length) || length < 1)
        {
            throw new ArgumentException("边缘对基准线必须有限且至少长1像素。");
        }

        if (caliperCount < 2 || caliperCount > 512 || normalHalfLength < 2 || normalHalfLength > 512
            || tangentHalfWidth < 0 || tangentHalfWidth > 32 || minimumPairs < 1 || minimumPairs > caliperCount)
        {
            throw new ArgumentOutOfRangeException(nameof(caliperCount));
        }

        if (!Enum.IsDefined(scanDirection) || !Enum.IsDefined(polarity) || !Enum.IsDefined(selection))
        {
            throw new ArgumentOutOfRangeException(nameof(polarity));
        }

        if (!double.IsFinite(minimumContrast) || minimumContrast <= 0 || minimumContrast > ushort.MaxValue
            || !double.IsFinite(gaussianSigmaPixels) || gaussianSigmaPixels < 0 || gaussianSigmaPixels > 10
            || gaussianSigmaPixels > 0 && gaussianSigmaPixels < 0.01
            || normalHalfLength <= Math.Ceiling(3 * gaussianSigmaPixels)
            || !double.IsFinite(minimumWidthPixels) || !double.IsFinite(maximumWidthPixels)
            || minimumWidthPixels <= 0 || maximumWidthPixels < minimumWidthPixels || maximumWidthPixels > 2 * normalHalfLength)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumWidthPixels), "宽度为扫描轴正距离闭区间，最大值不能超过搜索全长；阈值/sigma须与卡尺采样一致。");
        }

        if ((long)caliperCount * (2 * normalHalfLength + 1) * (2 * tangentHalfWidth + 1) > 5_000_000)
        {
            throw new ArgumentException("边缘对总采样数超过500万。");
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
                        throw new ArgumentException("整把边缘对卡尺必须位于图内，不截断或补边制造测宽边缘。");
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
        MinimumWidthPixels = minimumWidthPixels;
        MaximumWidthPixels = maximumWidthPixels;
        MinimumPairs = minimumPairs;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
        if (Image.Format == PixelFormat.Mono8 && MinimumContrast > byte.MaxValue)
        {
            issues.Add(new("ContrastRangeMismatch", "Mono8边缘对阈值不能超过255灰度差。"));
        }
    }
}
