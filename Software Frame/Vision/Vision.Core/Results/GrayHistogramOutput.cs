#nullable enable
using Vision.Core.Imaging;

namespace Vision.Core.Results;

/// <summary>
/// 独立的原编码灰度分箱快照；Bins是像素数量，不是概率/密度或像素面积测量。
/// Median保留分箱前的灰度值，不能仅从合并后的箱反推出它；不依赖图像或SDK上下文存活。
/// </summary>
public sealed class GrayHistogramOutput
{
    public Guid FrameId { get; }
    public Guid CoordinateSpaceId { get; }
    public PixelFormat Format { get; }
    public int GrayLevelCount => Format == PixelFormat.Mono8 ? 256 : 65536;
    public int BinWidth { get; }
    public IReadOnlyList<long> Bins { get; }
    public long PixelCount { get; }

    /// <summary>原灰度单位；偶数样本可为半整数，不是分箱标签或毫米单位。</summary>
    public double Median { get; }

    public GrayHistogramOutput(Guid frameId, Guid coordinateSpaceId, PixelFormat format,
        int binWidth, IEnumerable<long> bins, double median)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty)
        {
            throw new ArgumentException("直方图必须注明输入帧和像素空间。");
        }

        if (format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
        {
            throw new ArgumentOutOfRangeException(nameof(format));
        }

        var levels = format == PixelFormat.Mono8 ? 256 : 65536;
        if (binWidth < 1 || binWidth > levels)
        {
            throw new ArgumentOutOfRangeException(nameof(binWidth));
        }

        ArgumentNullException.ThrowIfNull(bins);
        var snapshot = bins.ToArray();
        if (snapshot.Length != (levels + binWidth - 1) / binWidth || snapshot.Any(value => value < 0))
        {
            throw new ArgumentException("直方图箱数或非负计数与位深/箱宽不符。", nameof(bins));
        }

        var count = snapshot.Aggregate(0L, (total, value) => checked(total + value));
        if (count == 0 || !double.IsFinite(median) || median < 0 || median >= levels
            || median * 2 != Math.Truncate(median * 2) || count % 2 == 1 && median != Math.Truncate(median))
        {
            throw new ArgumentException("中位数必须属于正数量整数样本；偶数样本允许半整数。", nameof(median));
        }

        // 这里只核验分箱能够证明的中位数范围，不能把箱中心当作原灰度中位数。
        var lowerRank = (count - 1) / 2;
        var upperRank = count / 2;
        long cumulative = 0;
        var lowerBin = -1;
        var upperBin = -1;
        for (var index = 0; index < snapshot.Length; index++)
        {
            cumulative += snapshot[index];
            if (lowerBin < 0 && cumulative > lowerRank)
            {
                lowerBin = index;
            }

            if (cumulative > upperRank)
            {
                upperBin = index;
                break;
            }
        }

        var minimumMedian = (lowerBin * binWidth + upperBin * binWidth) / 2d;
        var maximumMedian = (Math.Min((lowerBin + 1) * binWidth - 1, levels - 1)
            + Math.Min((upperBin + 1) * binWidth - 1, levels - 1)) / 2d;
        if (median < minimumMedian || median > maximumMedian)
        {
            throw new ArgumentException("中位数与中间顺序样本所在箱矛盾。", nameof(median));
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Format = format;
        BinWidth = binWidth;
        Bins = Array.AsReadOnly(snapshot);
        PixelCount = count;
        Median = median;
    }
}
