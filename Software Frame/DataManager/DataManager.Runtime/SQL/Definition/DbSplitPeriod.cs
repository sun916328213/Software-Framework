// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：DbSplitPeriod。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL、System.Reflection。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL;
using System.Reflection;

namespace DataManager.Runtime.SQL.Definition;

/// <summary>
/// P12：分表周期判定归属 Runtime（兼容 Dapper 旧特性需引用 Dapper 程序集）。
/// 命名空间保持不变；共享特性（<see cref="DbSplitTableAttribute"/>）仍在 Core。
/// </summary>
internal static class DbSplitPeriod
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PeriodOf。
    /// 正文中的关键协作调用包括 GetCustomAttribute；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SuffixFor。
    /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
    /// </summary>
    internal static string SuffixFor(SplitPeriod period, DateTime date) => period switch
    {
        SplitPeriod.Day => date.ToString("yyyyMMdd"),
        SplitPeriod.Month => date.ToString("yyyyMM"),
        SplitPeriod.Year => date.ToString("yyyy"),
        _ => throw new NotSupportedException($"不支持的分表类型: {period}")
    };
}
