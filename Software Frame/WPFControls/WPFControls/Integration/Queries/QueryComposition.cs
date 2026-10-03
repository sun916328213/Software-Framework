// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：QueryComposition。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.Log、DataManager.Core.Log.Definition、DataManager.Core.SQL、DataManager.Runtime.SQL、Prism.Ioc、WPFControls.Queries.Data。导入不代表所有类型都会在运行时加载。
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using Prism.Ioc;
using WPFControls.Queries.Data;
using WPFControls.Queries.Logs;
using WPFControls.ProgramControl.Models;

namespace WPFControls.Integration.Queries;

/// <summary>
/// QueryComposition 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// </summary>
internal static class QueryComposition
{
    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 Register。
    /// 正文中的关键协作调用包括 IsRegistered、RegisterSingleton；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Register(IContainerRegistry registry)
    {
        // 只提供通用查询服务和默认 reader；产能的具体数据库实体、reader/store 由机型宿主提供。
        // 每项注册都先检查已有契约，保留宿主定制实现，避免模块加载顺序把替换静默覆盖。
        if (!registry.IsRegistered(typeof(ITableQueryReader<>)))
            registry.RegisterSingleton(typeof(ITableQueryReader<>), typeof(SqlTableQueryReader<>));
        if (!registry.IsRegistered(typeof(ITableQueryService<>)))
            registry.RegisterSingleton(typeof(ITableQueryService<>), typeof(TableQueryService<>));
        if (!registry.IsRegistered<ICapacityStatisticsQueryService<CapacityStatisticsRow>>())
            registry.RegisterSingleton<ICapacityStatisticsQueryService<CapacityStatisticsRow>, CapacityStatisticsQueryService<CapacityStatisticsRow>>();
        if (!registry.IsRegistered<ICapacityStatisticsWriteService<CapacityStatisticsRow>>())
            registry.RegisterSingleton<ICapacityStatisticsWriteService<CapacityStatisticsRow>, CapacityStatisticsWriteService<CapacityStatisticsRow>>();
        if (!registry.IsRegistered<ILogHistoryReader<LogInfo>>())
            registry.RegisterSingleton<ILogHistoryReader<LogInfo>, RunLogHistoryReader>();
        if (!registry.IsRegistered<ILogHistoryReader<string>>())
            registry.RegisterSingleton<ILogHistoryReader<string>, SemiLogHistoryReader>();
        if (!registry.IsRegistered<ILogHistoryQueryService<LogInfo>>())
            registry.RegisterSingleton<ILogHistoryQueryService<LogInfo>, LogHistoryQueryService<LogInfo>>();
        if (!registry.IsRegistered<ILogHistoryQueryService<string>>())
            registry.RegisterSingleton<ILogHistoryQueryService<string>, LogHistoryQueryService<string>>();
    }
}
