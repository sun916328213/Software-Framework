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
/// 固定或定位跟随的圆/圆弧卡尺请求；正角为屏幕顺时针，全360度不重复首尾，部分弧包含两端。
/// 绑定时圆心/半径/起始角为模板局部基准，中心映射、半径按正等比缩放、起始角加定位转角。
/// 径向1像素采样；采样宽高、sigma、残差等限始终为当前图像像素，不自动缩放或毫米标定。
/// </summary>
public sealed class CaliperCircleOperation : VisionImageOperation<CaliperCircleOutput>
{
    public const string Id = "vision.measurement.caliper-circle";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    /// <summary>绑定时为模板局部基准；不修改原始圆心/半径/起始角。</summary>
    public Point2D ReferenceCenter
    {
        get;
    }

    public double ReferenceNominalRadiusPixels
    {
        get;
    }

    public double ReferenceAngleStartDegrees
    {
        get;
    }

    public TemplateLocationBinding? LocationBinding
    {
        get;
    }

    public Point2D Center
    {
        get;
    }

    public double NominalRadiusPixels
    {
        get;
    }

    public double AngleStartDegrees
    {
        get;
    }

    public double AngleExtentDegrees
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

    public CircleScanDirection ScanDirection
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

    public double MaximumRadialResidualPixels
    {
        get;
    }

    public double MinimumCoverageDegrees
    {
        get;
    }

    public CaliperCircleOperation(VisionFrame image, Point2D center, double nominalRadiusPixels,
        int caliperCount = 32, int normalHalfLength = 8, int tangentHalfWidth = 1,
        CircleScanDirection scanDirection = CircleScanDirection.Outward,
        CaliperEdgePolarity polarity = CaliperEdgePolarity.Any,
        CaliperEdgeSelection selection = CaliperEdgeSelection.Strongest,
        double minimumContrast = 10, double gaussianSigmaPixels = 1, int minimumPoints = 6,
        double maximumRadialResidualPixels = 1, double angleStartDegrees = 0,
        double angleExtentDegrees = 360, double minimumCoverageDegrees = 90,
        TemplateLocationBinding? locationBinding = null) : base(image)
    {
        ReferenceCenter = center;
        ReferenceNominalRadiusPixels = nominalRadiusPixels;
        ReferenceAngleStartDegrees = angleStartDegrees;
        LocationBinding = locationBinding;
        if (locationBinding != null)
        {
            var candidate = locationBinding.Resolve(image);
            center = candidate.TemplateToImage.Transform(center);
            nominalRadiusPixels *= candidate.Scale;
            angleStartDegrees += candidate.AngleDegrees;
        }

        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || !double.IsFinite(nominalRadiusPixels)
            || nominalRadiusPixels <= normalHalfLength)
        {
            throw new ArgumentException("名义圆必须有限，半径须大于径向搜索半长，不跨圆心采样。");
        }

        if (caliperCount < 3 || caliperCount > 512 || normalHalfLength < 2 || normalHalfLength > 512
            || tangentHalfWidth < 0 || tangentHalfWidth > 32 || minimumPoints < 3 || minimumPoints > caliperCount)
        {
            throw new ArgumentOutOfRangeException(nameof(caliperCount));
        }

        if (!Enum.IsDefined(scanDirection) || !Enum.IsDefined(polarity) || !Enum.IsDefined(selection))
        {
            throw new ArgumentOutOfRangeException(nameof(scanDirection));
        }

        if (!double.IsFinite(minimumContrast) || minimumContrast <= 0 || minimumContrast > ushort.MaxValue
            || !double.IsFinite(gaussianSigmaPixels) || gaussianSigmaPixels < 0 || gaussianSigmaPixels > 10
            || gaussianSigmaPixels > 0 && gaussianSigmaPixels < 0.01
            || normalHalfLength <= Math.Ceiling(3 * gaussianSigmaPixels)
            || !double.IsFinite(maximumRadialResidualPixels) || maximumRadialResidualPixels <= 0
            || !double.IsFinite(angleStartDegrees) || !double.IsFinite(angleExtentDegrees)
            || angleExtentDegrees < 1 || angleExtentDegrees > 360
            || !double.IsFinite(minimumCoverageDegrees) || minimumCoverageDegrees <= 0
            || minimumCoverageDegrees >= 360 || minimumCoverageDegrees > angleExtentDegrees)
        {
            throw new ArgumentOutOfRangeException(nameof(angleExtentDegrees), "阈值、平滑、残差及圆弧/覆盖角范围无效。");
        }

        if ((long)caliperCount * (2 * normalHalfLength + 1) * (2 * tangentHalfWidth + 1) > 5_000_000)
        {
            throw new ArgumentException("圆卡尺总采样超过500万。");
        }

        Center = center;
        NominalRadiusPixels = nominalRadiusPixels;
        var normalizedStart = angleStartDegrees % 360;
        AngleStartDegrees = normalizedStart < 0 ? normalizedStart + 360 : normalizedStart;
        AngleExtentDegrees = angleExtentDegrees;
        CaliperCount = caliperCount;
        NormalHalfLength = normalHalfLength;
        TangentHalfWidth = tangentHalfWidth;
        ScanDirection = scanDirection;
        Polarity = polarity;
        Selection = selection;
        MinimumContrast = minimumContrast;
        GaussianSigmaPixels = gaussianSigmaPixels;
        MinimumPoints = minimumPoints;
        MaximumRadialResidualPixels = maximumRadialResidualPixels;
        MinimumCoverageDegrees = minimumCoverageDegrees;
        for (var index = 0; index < CaliperCount; index++)
        {
            var placement = Placement(index);
            foreach (var u in new[] { -tangentHalfWidth, tangentHalfWidth })
            {
                foreach (var v in new[] { -normalHalfLength, normalHalfLength })
                {
                    var x = placement.Center.X + u * placement.Tangent.X + v * placement.Normal.X;
                    var y = placement.Center.Y + u * placement.Tangent.Y + v * placement.Normal.Y;
                    if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 || x > image.Width - 1 || y > image.Height - 1)
                    {
                        throw new ArgumentException("每把圆卡尺必须完整位于图内，不裁切或借补边制造圆边缘。");
                    }
                }
            }
        }
    }

    /// <summary>只返回不可变请求的纯几何放置；不依赖厂商对象或可编辑ROI。</summary>
    public (Point2D Center, Point2D Tangent, Point2D Normal) Placement(int index)
    {
        if (index < 0 || index >= CaliperCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var denominator = AngleExtentDegrees == 360 ? CaliperCount : CaliperCount - 1;
        var angle = AngleStartDegrees + AngleExtentDegrees * index / denominator;
        var (cos, sin) = RotationMath.SinCosDegrees(angle);
        var sign = ScanDirection == CircleScanDirection.Outward ? 1 : -1;
        return (Center.Offset(NominalRadiusPixels * cos, NominalRadiusPixels * sin),
            new Point2D(-sin, cos), new Point2D(sign * cos, sign * sin));
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
        if (Image.Format == PixelFormat.Mono8 && MinimumContrast > byte.MaxValue)
        {
            issues.Add(new("ContrastRangeMismatch", "Mono8圆卡尺阈值不能超过255灰度差。"));
        }
    }
}
