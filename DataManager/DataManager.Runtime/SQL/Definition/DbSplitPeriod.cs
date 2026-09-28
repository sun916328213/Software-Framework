using System.Reflection;

namespace DataManager.SQL.Definition;

/// <summary>
/// P12：分表周期判定归属 Runtime（兼容 Dapper 旧特性需引用 Dapper 程序集）。
/// 命名空间保持不变；共享特性（<see cref="DbSplitTableAttribute"/>）仍在 Core。
/// </summary>
internal static class DbSplitPeriod
{
    internal static SplitPeriod PeriodOf(Type entityType)
    {
        var shared = entityType.GetCustomAttribute<DbSplitTableAttribute>();
        if (shared != null)
            return shared.Period;
        var legacy = entityType.GetCustomAttribute<Dapper.DapperSplitTableAttribute>()
            ?? throw new InvalidOperationException($"实体 {entityType.Name} 未标记分表特性");
        return legacy.SplitType switch
        {
            Dapper.SplitType.Day => SplitPeriod.Day,
            Dapper.SplitType.Month => SplitPeriod.Month,
            Dapper.SplitType.Year => SplitPeriod.Year,
            _ => throw new NotSupportedException($"不支持的分表类型: {legacy.SplitType}")
        };
    }

    internal static string SuffixFor(SplitPeriod period, DateTime date) => period switch
    {
        SplitPeriod.Day => date.ToString("yyyyMMdd"),
        SplitPeriod.Month => date.ToString("yyyyMM"),
        SplitPeriod.Year => date.ToString("yyyy"),
        _ => throw new NotSupportedException($"不支持的分表类型: {period}")
    };
}
