#nullable enable
using Vision.Core.Imaging;

namespace Vision.Core.Results;

/// <summary>
/// 对应本次输入帧的有效参与域灰度统计。
/// 总体标准差为像素与均值差值的平方和除以像素总数，再取平方根。
/// </summary>
public sealed class GrayStatisticsOutput
{
    /// <summary>
    /// 本次统计输入帧身份，统计不创建派生图像。
    /// </summary>
    public Guid FrameId
    {
        get;
    }

    /// <summary>
    /// 本次结果所使用的像素坐标空间标识。
    /// </summary>
    public Guid CoordinateSpaceId
    {
        get;
    }

    /// <summary>输入原始灰度位深，不是输出显示格式。</summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// 本次参与统计的像素数量。
    /// </summary>
    public long PixelCount
    {
        get;
    }

    /// <summary>
    /// 本次观测到的最小灰度值。
    /// </summary>
    public double Minimum
    {
        get;
    }

    /// <summary>
    /// 本次观测到的最大灰度值。
    /// </summary>
    public double Maximum
    {
        get;
    }

    /// <summary>
    /// 参与统计像素的平均灰度值。
    /// </summary>
    public double Mean
    {
        get;
    }

    /// <summary>
    /// 总体标准差，方差计算以像素总数作为分母。
    /// </summary>
    public double PopulationStandardDeviation
    {
        get;
    }

    /// <summary>
    /// 创建参与域统计结果，拒绝无效身份、数量及非有限统计值。
    /// </summary>
    public GrayStatisticsOutput(
        Guid frameId,
        Guid coordinateSpaceId,
        PixelFormat format,
        long pixelCount,
        double minimum,
        double maximum,
        double mean,
        double populationStandardDeviation)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty)
        {
            throw new ArgumentException("结果必须注明输入帧和坐标空间。");
        }

        if (format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
        {
            throw new ArgumentOutOfRangeException(nameof(format));
        }

        if (pixelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelCount));
        }

        if (!double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || minimum < 0
            || maximum > (format == PixelFormat.Mono8 ? byte.MaxValue : ushort.MaxValue)
            || minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum));
        }

        if (!double.IsFinite(mean) || mean < minimum || mean > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(mean));
        }

        if (!double.IsFinite(populationStandardDeviation) || populationStandardDeviation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(populationStandardDeviation));
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Format = format;
        PixelCount = pixelCount;
        Minimum = minimum;
        Maximum = maximum;
        Mean = mean;
        PopulationStandardDeviation = populationStandardDeviation;
    }
}
