// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmHistoryRow、AlarmHistorySummary、AlarmHistoryResult、IAlarmHistoryReader、IAlarmHistoryQueryService、AlarmHistoryQueryService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Globalization。导入不代表所有类型都会在运行时加载。
using System.Globalization;

namespace WPFControls.Queries.Alarms;

/// <summary>
/// AlarmHistoryRow 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public sealed record AlarmHistoryRow(int ID, string Info, string StartTime, string EndTime, string Duration);
/// <summary>
/// AlarmHistorySummary 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public sealed record AlarmHistorySummary(string Info, int Count, double DurationSeconds, double Proportion);
/// <summary>
/// AlarmHistoryResult 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public sealed record AlarmHistoryResult(IReadOnlyList<AlarmHistoryRow> Rows, IReadOnlyList<AlarmHistorySummary> Summaries);

/// <summary>
/// IAlarmHistoryReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface IAlarmHistoryReader
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadDayAsync。
    /// </summary>
    Task<IReadOnlyList<AlarmHistoryRow>> ReadDayAsync(DateTime day, CancellationToken cancellationToken);
}

/// <summary>
/// IAlarmHistoryQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface IAlarmHistoryQueryService
{
    /// <summary>
    /// 执行查询操作；对应入口为 QueryAsync。
    /// </summary>
    Task<AlarmHistoryResult> QueryAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
}

/// <summary>Stateless query and aggregation, independent of WPF, Prism and persistence implementations.</summary>
/// <remarks>
/// AlarmHistoryQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// 继承或实现的契约：IAlarmHistoryQueryService。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class AlarmHistoryQueryService(IAlarmHistoryReader reader) : IAlarmHistoryQueryService
{
    /// <summary>
    /// 执行查询操作；对应入口为 QueryAsync。
    /// 正文中的关键协作调用包括 nameof、ThrowIfCancellationRequested、ConfigureAwait、ReadDayAsync、AddRange；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task<AlarmHistoryResult> QueryAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (start.Date > end.Date)
            throw new ArgumentException("查询开始日期不能晚于结束日期。", nameof(start));
        var rows = new List<AlarmHistoryRow>();
        var day = start.Date;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dailyRows = await reader.ReadDayAsync(day, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            rows.AddRange(dailyRows.Where(row => !string.IsNullOrEmpty(row.Info)));
            if (day == end.Date) break;
            day = day.AddDays(1);
        }
        var summaries = rows.GroupBy(row => row.Info).Select(group => new AlarmHistorySummary(
            group.Key, group.Count(), group.Sum(row => ParseSeconds(row.Duration)),
            (double)group.Count() / rows.Count)).ToArray();
        return new AlarmHistoryResult(rows.AsReadOnly(), Array.AsReadOnly(summaries));
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ParseSeconds。
    /// 正文中的关键协作调用包括 Trim、EndsWith、TryParse、IsFinite；具体条件和先后次序见方法体。
    /// </summary>
    private static double ParseSeconds(string duration)
    {
        var text = duration.Trim();
        if (text.EndsWith(" s", StringComparison.Ordinal)) text = text[..^2];
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return double.IsFinite(value) && value >= 0 ? value : throw new FormatException($"无效报警时长：{duration}");
        throw new FormatException($"无效报警时长：{duration}");
    }
}
