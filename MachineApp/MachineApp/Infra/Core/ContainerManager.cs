// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：ContainerManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Ioc。导入不代表所有类型都会在运行时加载。
using Prism.Ioc;

namespace MachineApp.Infra.Core;

/// <summary>
/// ContainerManager 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// </summary>
public static class ContainerManager
{
    private static IContainerProvider? _container;

    /// <summary>
    /// 执行初始化步骤；对应入口为 Initialize。
    /// </summary>
    public static void Initialize(IContainerProvider container)
    {
        _container = container;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Resolve。
    /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
    /// </summary>
    public static T Resolve<T>()
    {
        if (_container == null)
            throw new InvalidOperationException("ContainerManager 尚未初始化，请在 App.OnInitialized 中调用 ContainerManager.Initialize(Container)");
        return _container.Resolve<T>();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Resolve。
    /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
    /// </summary>
    public static object Resolve(Type type)
    {
        if (_container == null)
            throw new InvalidOperationException("ContainerManager 尚未初始化");
        return _container.Resolve(type);
    }

    public static bool IsInitialized => _container != null;
}
