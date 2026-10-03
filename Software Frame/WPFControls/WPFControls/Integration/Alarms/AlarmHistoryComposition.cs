// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：AlarmHistoryComposition。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Prism.Ioc、WPFControls.Queries.Alarms。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Prism.Ioc;
using WPFControls.Queries.Alarms;

namespace WPFControls.Integration.Alarms;

/// <summary>
/// AlarmHistoryComposition 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// </summary>
internal static class AlarmHistoryComposition
{
    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 Register。
    /// 正文中的关键协作调用包括 IsRegistered、RegisterSingleton、getDirectory；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Register(IContainerRegistry registry, Func<IContainerProvider, string> getDirectory)
    {
        if (!registry.IsRegistered<IAlarmHistoryReader>())
            registry.RegisterSingleton<IAlarmHistoryReader>(provider =>
            new ExcelAlarmHistoryReader(() => getDirectory(provider)));
        if (!registry.IsRegistered<IAlarmHistoryQueryService>())
            registry.RegisterSingleton<IAlarmHistoryQueryService, AlarmHistoryQueryService>();
    }
}
