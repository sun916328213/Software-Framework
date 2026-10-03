// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：PackingPersistenceComposition。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.SQL、MachineApp.Infra.Persistence.Adapters、MachineApp.Infra.Persistence.Entities、Prism.Ioc、WPFControls.ProgramControl.Models、WPFControls.Queries.Data。导入不代表所有类型都会在运行时加载。
using DataManager.Core.SQL;
using MachineApp.Infra.Persistence.Adapters;
using MachineApp.Infra.Persistence.Entities;
using Prism.Ioc;
using WPFControls.ProgramControl.Models;
using WPFControls.Queries.Data;

namespace MachineApp.Infra.Persistence;

/// <summary>
/// PackingPersistenceComposition 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// </summary>
internal static class PackingPersistenceComposition
{
    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterCapacity。
    /// 正文中的关键协作调用包括 IsRegistered、RegisterSingleton、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    internal static void RegisterCapacity(IContainerRegistry registry)
    {
        // UI 只认识 CapacityStatisticsRow；具体 SQL 泛型实体留在宿主，避免通用页面绑定机型表类型。
        // 两个端口分别负责读取与保存。宿主或测试已提供端口时，不覆盖其替代实现。
        // 使用工厂延迟取得 SQL 对象：容器注册阶段不访问数据库，也不抢在数据库配置之前创建实例。
        if (!registry.IsRegistered<ICapacityStatisticsReader<CapacityStatisticsRow>>())
            registry.RegisterSingleton<ICapacityStatisticsReader<CapacityStatisticsRow>>(provider =>
                new PackingCapacityStatisticsReader(() => provider.Resolve<ISQL<CapacityStatisticsInfo>>()));
        if (!registry.IsRegistered<ICapacityStatisticsStore<CapacityStatisticsRow>>())
            registry.RegisterSingleton<ICapacityStatisticsStore<CapacityStatisticsRow>>(provider =>
                new PackingCapacityStatisticsStore(() => provider.Resolve<ISQL<CapacityStatisticsInfo>>()));
    }
}
