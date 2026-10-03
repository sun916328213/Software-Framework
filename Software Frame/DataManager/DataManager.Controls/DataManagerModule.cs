// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：DataManagerModule、DataManagerModuleOptions。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls.Models、DataManager.Controls.Services、DataManager.Controls.ViewModels、DataManager.Controls.Views、DataManager.Core.Excel、DataManager.Runtime.Excel。导入不代表所有类型都会在运行时加载。
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using DataManager.Controls.Views;
using DataManager.Core.Excel;
using DataManager.Runtime.Excel;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Runtime;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;

namespace DataManager.Controls;

/// <summary>
/// DataManager 通用装配入口（Framework-Refactoring §4.4）。
/// 统一注册本模块通用实现（Manager）、Workspace 与页面；实现保留在 Runtime，契约在 Core。
/// 依赖方向：Controls → Runtime → Core；Runtime 不反向依赖 Controls。
/// </summary>
public sealed class DataManagerModule : IModule
{
    /// <summary>
    /// 模块初始化：调用 Runtime 显式初始化入口（幂等，不打开硬件/数据库/业务文件）。
    /// </summary>
    public void OnInitialized(IContainerProvider provider) =>
        DataManagerRuntime.Initialize();

    /// <summary>公共服务及非泛型注册：Manager 单例 + LogWorkspace。与显式泛型入口共用，不重复建实例。</summary>
    public void RegisterTypes(IContainerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.IsRegistered<ILogProviderFactory>()) registry.RegisterSingleton<ILogProviderFactory, LogProviderFactory>();
        if (!registry.IsRegistered<ISqlProviderFactory>()) registry.RegisterSingleton<ISqlProviderFactory, SqlProviderFactory>();
        if (!registry.IsRegistered<IExcelProviderFactory>()) registry.RegisterSingleton<IExcelProviderFactory, ExcelProviderFactory>();
        RegisterManager<LogManager, ILogManager>(registry, provider => new LogManager(provider.Resolve<ILogProviderFactory>()));
        RegisterManager<SQLManager, ISQLManager>(registry, provider => new SQLManager(provider.Resolve<ISqlProviderFactory>()));
        RegisterManager<ExcelManager, IExcelManager>(registry, provider => new ExcelManager(provider.Resolve<IExcelProviderFactory>()));

        if (!registry.IsRegistered<LogWorkspace>())
            registry.RegisterSingleton<LogWorkspace>();
    }

    /// <summary>
    /// 默认 Manager 注册：接口与实现共享同一个实例。
    /// 宿主已明确提供替换（契约已注册）时不覆盖；也不假设替换对象属于默认实现类，不补具体类型映射。
    /// </summary>
    private static void RegisterManager<TManager, TContract>(IContainerRegistry registry, Func<IContainerProvider, TManager> factory)
        where TManager : TContract
    {
        if (registry.IsRegistered<TContract>())
            return;
        if (registry.IsRegistered<TManager>())
        {
            registry.RegisterSingleton<TContract>(provider => provider.Resolve<TManager>());
            return;
        }
        registry.RegisterSingleton<TManager>(provider => factory(provider));
        registry.RegisterSingleton<TContract>(provider => provider.Resolve<TManager>());
    }

}

/// <summary>Host navigation policy; omitted configuration uses transient Workbench pages.</summary>
/// <remarks>
/// DataManagerModuleOptions 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// </remarks>
public sealed class DataManagerModuleOptions
{
    /// <summary>Whether navigation reuses page instances for production prewarming.</summary>
    public bool ReuseNavigationViews { get; init; }
}

/// <summary>Prism module that composes services and pages for host-owned SQL and Excel entities.</summary>
/// <typeparam name="TSqlEntity">Host SQL entity.</typeparam>
/// <typeparam name="TExcelRow">Host Excel row.</typeparam>
/// <remarks>
/// DataManagerModule 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：IModule。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class DataManagerModule<TSqlEntity, TExcelRow> : IModule
    where TSqlEntity : class, ISqlEntity, new()
    where TExcelRow : class, IExcelTestEntity, new()
{
    private readonly bool _reuseNavigationViews;

    /// <summary>Creates the module with an optional host navigation policy.</summary>
    /// <remarks>
    /// 初始化 DataManagerModule 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </remarks>
    public DataManagerModule(DataManagerModuleOptions? options = null) =>
        _reuseNavigationViews = options?.ReuseNavigationViews ?? false;

    /// <summary>Registers shared services, typed workspaces and all three pages.</summary>
    /// <remarks>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 RegisterTypes、IsRegistered、RegisterSingleton、RegisterPage；具体条件和先后次序见方法体。
    /// </remarks>
    public void RegisterTypes(IContainerRegistry registry)
    {
        new DataManagerModule().RegisterTypes(registry);
        if (!registry.IsRegistered<SqlWorkspace<TSqlEntity>>()) registry.RegisterSingleton<SqlWorkspace<TSqlEntity>>();
        if (!registry.IsRegistered<ExcelWorkspace<TExcelRow>>()) registry.RegisterSingleton<ExcelWorkspace<TExcelRow>>();
        RegisterPage<SqlView, SqlViewModel<TSqlEntity>>(registry);
        RegisterPage<ExcelView, ExcelViewModel<TExcelRow>>(registry);
        RegisterPage<LogView, LogViewModel>(registry);
    }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterPage。
    /// 正文中的关键协作调用包括 IsRegistered、RegisterSingleton、Register、RegisterForNavigation；具体条件和先后次序见方法体。
    /// </summary>
    private void RegisterPage<TView, TViewModel>(IContainerRegistry registry) where TView : FrameworkElement
    {
        if (registry.IsRegistered(typeof(object), typeof(TView).Name)) return;
        if (_reuseNavigationViews)
        {
            registry.RegisterSingleton(typeof(object), typeof(TView), typeof(TView).Name);
            Prism.Mvvm.ViewModelLocationProvider.Register<TView, TViewModel>();
        }
        else registry.RegisterForNavigation<TView, TViewModel>();
    }

    /// <summary>Initializes runtime switches without opening external resources.</summary>
    /// <remarks>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// 正文中的关键协作调用包括 Initialize；具体条件和先后次序见方法体。
    /// </remarks>
    public void OnInitialized(IContainerProvider provider) => DataManagerRuntime.Initialize();
}
