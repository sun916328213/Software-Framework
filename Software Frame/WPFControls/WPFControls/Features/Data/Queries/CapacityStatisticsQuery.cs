// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：CapacityStatisticsEntry、CapacityTrendPoint、CapacityStatisticsResult、ICapacityStatisticsReader、ICapacityStatisticsQueryService、CapacityStatisticsQueryService。成员说明可结合下方具体实现阅读。
namespace WPFControls.Queries.Data;

/// <summary>
/// CapacityStatisticsEntry 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public sealed record CapacityStatisticsEntry<T>(T Row, DateTime Time, IReadOnlyList<int> Good, IReadOnlyList<int> Defect);
/// <summary>
/// CapacityTrendPoint 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public sealed record CapacityTrendPoint(string Label, int Good, int Defect, int Total);
/// <summary>
/// CapacityStatisticsResult 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public sealed record CapacityStatisticsResult<T>(IReadOnlyList<T> Rows, int GoodCount, int DefectCount,
    int TotalOutput, double YieldRate, int RunTime, double UtilizationRate, IReadOnlyList<CapacityTrendPoint> Trend);

/// <summary>
/// ICapacityStatisticsReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface ICapacityStatisticsReader<T>
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 Read。
    /// </summary>
    IReadOnlyList<CapacityStatisticsEntry<T>> Read(DateTime start, DateTime end);
}

/// <summary>
/// ICapacityStatisticsQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface ICapacityStatisticsQueryService<T>
{
    /// <summary>
    /// 执行查询操作；对应入口为 Query。
    /// </summary>
    CapacityStatisticsResult<T> Query(DateTime start, DateTime end);
}

/// <summary>Existing statistics formulas and trend grouping, independent of persistence and charts.</summary>
/// <remarks>
/// CapacityStatisticsQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// 继承或实现的契约：ICapacityStatisticsQueryService&lt;T&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class CapacityStatisticsQueryService<T>(ICapacityStatisticsReader<T> reader) : ICapacityStatisticsQueryService<T>
{
    /// <summary>
    /// 执行查询操作；对应入口为 Query。
    /// 正文中的关键协作调用包括 AddDays、Read、Sum、Add、ToDictionary；具体条件和先后次序见方法体。
    /// </summary>
    public CapacityStatisticsResult<T> Query(DateTime start, DateTime end)
    {
        var endDay = end.AddDays(1);
        var data = reader.Read(start, endDay);
        int good = data.Sum(row => row.Good.Sum());
        int defect = data.Sum(row => row.Defect.Sum());
        int total = good + defect;
        int activeHours = 0;
        foreach (var row in data)
            for (int hour = 0; hour < 24; hour++)
                if (row.Good[hour] + row.Defect[hour] > 0) activeHours++;
        double totalHours = (endDay - start).TotalHours + 24;
        var trend = new List<CapacityTrendPoint>();
        if ((endDay - start).TotalDays <= 2)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                int ok = data.Sum(row => row.Good[hour]);
                int ng = data.Sum(row => row.Defect[hour]);
                trend.Add(new($"{hour:00}:00", ok, ng, data.Sum(row => row.Good[hour] + row.Defect[hour])));
            }
        }
        else
        {
            var byDay = data.GroupBy(row => row.Time.Date).ToDictionary(group => group.Key, group => group.ToList());
            for (var day = start; day <= endDay; day = day.AddDays(1))
            {
                int ok = 0, ng = 0, output = 0;
                if (byDay.TryGetValue(day, out var rows))
                {
                    ok = rows.Sum(row => row.Good.Sum());
                    ng = rows.Sum(row => row.Defect.Sum());
                    output = rows.Sum(row => row.Good.Sum() + row.Defect.Sum());
                }
                trend.Add(new(day.ToString("MM-dd"), ok, ng, output));
            }
        }
        return new(Array.AsReadOnly(data.Select(row => row.Row).ToArray()), good, defect, total,
            total > 0 ? Math.Round(good * 100.0 / total, 2) : 0, activeHours,
            totalHours > 0 ? Math.Round(activeHours * 100.0 / totalHours, 2) : 0, trend.AsReadOnly());
    }
}
