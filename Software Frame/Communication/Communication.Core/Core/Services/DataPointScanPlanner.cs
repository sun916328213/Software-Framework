// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DataPointScanPlan、DataPointScanPlanner。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Interfaces;

namespace Communication.Core.Services;

/// <summary>表示某一时刻的数据点扫描计划。</summary>
public sealed record DataPointScanPlan
{
    /// <summary>创建扫描计划。</summary>
    public DataPointScanPlan(
        IReadOnlyList<IDataPoint> duePoints,
        TimeSpan delayUntilNextScan)
    {
        DuePoints = duePoints;
        DelayUntilNextScan = delayUntilNextScan;
    }

    /// <summary>当前已到期的可读点位。</summary>
    public IReadOnlyList<IDataPoint> DuePoints { get; }

    /// <summary>到下一个点位到期的等待时间。</summary>
    public TimeSpan DelayUntilNextScan { get; }
}

/// <summary>根据点位最后采样时间和扫描周期计算下一轮采样。</summary>
public static class DataPointScanPlanner
{
    /// <summary>计算当前到期点位与下一次唤醒延迟。</summary>
    public static DataPointScanPlan Plan(
        IEnumerable<IDataPoint> points,
        DateTimeOffset now,
        TimeSpan defaultInterval)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (defaultInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultInterval),
                "默认扫描周期必须大于零。");
        }

        var duePoints = new List<IDataPoint>();
        var shortestDelay = defaultInterval;
        foreach (var point in points.Where(static point => point.Readable))
        {
            if (point.ScanIntervalMs < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(points),
                    $"数据点 {point.Name} 的扫描周期不能为负数。");
            }

            var interval = point.ScanIntervalMs == 0
                ? defaultInterval
                : TimeSpan.FromMilliseconds(point.ScanIntervalMs);
            if (point.Current.Timestamp == DateTimeOffset.MinValue)
            {
                duePoints.Add(point);
                continue;
            }

            var remaining = point.Current.Timestamp + interval - now;
            if (remaining <= TimeSpan.Zero)
            {
                duePoints.Add(point);
            }
            else if (remaining < shortestDelay)
            {
                shortestDelay = remaining;
            }
        }

        return new DataPointScanPlan(
            duePoints,
            duePoints.Count > 0 ? TimeSpan.Zero : shortestDelay);
    }
}
