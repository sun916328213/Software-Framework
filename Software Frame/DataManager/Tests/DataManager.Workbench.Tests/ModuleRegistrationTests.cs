// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModuleRegistrationTests、ModuleWorkbenchCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Controls、DataManager.Controls.Services、DataManager.Core.Excel、DataManager.Runtime.Excel。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Controls;
using DataManager.Controls.Services;
using DataManager.Core.Excel;
using DataManager.Runtime.Excel;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Runtime;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Workbench.Models;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;
using System.Reflection.PortableExecutable;

namespace DataManager.Workbench.Tests;

/// <summary>
/// DataManager 模块装配测试（Framework-Refactoring §4.4 / §8 IOC 验收）。
/// 由 DataManager.Tests 逻辑线迁入 WPF 线：模块实现已归属 DataManager.Controls（WPF 程序集），
/// 逻辑线不再为验证模块而引用 Controls（见 LayeringTests）。
/// </summary>
public sealed class ModuleRegistrationTests
{
    /// <summary>模块类型直接实现 Prism 的 IModule 契约（由 SqlDependencyArchitectureTests.Prism_RemainsDirectlyReferenced 迁入）。</summary>
    [Fact]
    public void Module_ImplementsPrismIModule()
    {
        Assert.Contains(typeof(Prism.Modularity.IModule), typeof(DataManagerModule).GetInterfaces());
    }

    /// <summary>Controls 程序集保持 AnyCPU（由 SqlDependencyArchitectureTests.Assemblies_StayAnyCpu_NotX64 迁入）。</summary>
    [Fact]
    public void ControlsAssembly_StaysAnyCpu_NotX64()
    {
        using var stream = File.OpenRead(typeof(DataManagerModule).Assembly.Location);
        using var pe = new PEReader(stream);
        Assert.NotEqual(Machine.Amd64, pe.PEHeaders.CoffHeader.Machine);
        Assert.NotEqual(Machine.IA64, pe.PEHeaders.CoffHeader.Machine);
        Assert.True(pe.PEHeaders.CorHeader!.Flags.HasFlag(CorFlags.ILOnly));
        Assert.False(pe.PEHeaders.CorHeader.Flags.HasFlag(CorFlags.Requires32Bit));
    }

    /// <summary>
    /// 测试相关入口 ContractAndImplementation_ShareSameInstance；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewContainer、RegisterTypes、Same、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ContractAndImplementation_ShareSameInstance()
    {
        var container = NewContainer();
        new DataManagerModule().RegisterTypes(container);

        Assert.Same(container.Resolve<LogManager>(), container.Resolve<ILogManager>());
        Assert.Same(container.Resolve<SQLManager>(), container.Resolve<ISQLManager>());
        Assert.Same(container.Resolve<ExcelManager>(), container.Resolve<IExcelManager>());
        Assert.Same(container.Resolve<LogWorkspace>(), container.Resolve<LogWorkspace>());
    }

    /// <summary>
    /// 测试相关入口 ModuleAndExplicitRegister_DoNotDuplicateInstances；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewContainer、RegisterTypes、Resolve、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ModuleAndExplicitRegister_DoNotDuplicateInstances()
    {
        var container = NewContainer();
        new DataManagerModule().RegisterTypes(container);
        var before = container.Resolve<ISQLManager>();

        new DataManagerModule<TestEntity, ExcelPerson>().RegisterTypes(container);
        Assert.Same(before, container.Resolve<ISQLManager>());
        Assert.Same(container.Resolve<LogManager>(), container.Resolve<ILogManager>());

        // 泛型入口先行、模块后加载同样不重复建实例。
        var second = NewContainer();
        new DataManagerModule<TestEntity, ExcelPerson>().RegisterTypes(second);
        var secondBefore = second.Resolve<IExcelManager>();
        new DataManagerModule().RegisterTypes(second);
        Assert.Same(secondBefore, second.Resolve<IExcelManager>());
    }

    /// <summary>
    /// 测试相关入口 HostReplacement_IsNotOverriddenByDefaults；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewContainer、RegisterInstance、RegisterTypes、Same、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HostReplacement_IsNotOverriddenByDefaults()
    {
        var container = NewContainer();
        var stub = new SQLManager();
        container.RegisterInstance<ISQLManager>(stub);

        new DataManagerModule<TestEntity, ExcelPerson>().RegisterTypes(container);
        Assert.Same(stub, container.Resolve<ISQLManager>());
    }

    /// <summary>
    /// 测试相关入口 SeparateContainers_DoNotShareStaticState；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewContainer、RegisterTypes、NotSame、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SeparateContainers_DoNotShareStaticState()
    {
        var first = NewContainer();
        var second = NewContainer();
        new DataManagerModule().RegisterTypes(first);
        new DataManagerModule().RegisterTypes(second);

        Assert.NotSame(first.Resolve<ILogManager>(), second.Resolve<ILogManager>());
        Assert.NotSame(first.Resolve<LogWorkspace>(), second.Resolve<LogWorkspace>());
    }

    /// <summary>
    /// 测试相关入口 NativePrismRegistrationProvidesAllPageKeys；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewContainer、RegisterTypes、True、IsRegistered、nameof；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NativePrismRegistrationProvidesAllPageKeys()
    {
        var container = NewContainer();
        new DataManagerModule<TestEntity, ExcelPerson>().RegisterTypes(container);

        new DataManagerModule<TestEntity, ExcelPerson>().RegisterTypes(container);
        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.SqlView)));
        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.ExcelView)));
        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.LogView)));
    }

    /// <summary>
    /// 测试相关入口 ReuseModes_RegisterNavigationKeys；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewContainer、RegisterTypes、True、IsRegistered、nameof；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReuseModes_RegisterNavigationKeys(bool reuseNavigationViews)
    {
        var container = NewContainer();
        new DataManagerModule<TestEntity, ExcelPerson>(new DataManagerModuleOptions { ReuseNavigationViews = reuseNavigationViews }).RegisterTypes(container);

        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.SqlView)));
        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.LogView)));
        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.ExcelView)));
        Assert.True(((IContainerRegistry)container).IsRegistered<SqlWorkspace<TestEntity>>());
        Assert.True(((IContainerRegistry)container).IsRegistered<ExcelWorkspace<ExcelPerson>>());
    }

    /// <summary>
    /// 测试相关入口 OnInitialized_IsIdempotentAndKeepsLegacyTimestampBehavior；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewContainer、OnInitialized、True、TryGetSwitch、Initialize；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OnInitialized_IsIdempotentAndKeepsLegacyTimestampBehavior()
    {
        var container = NewContainer();
        var module = new DataManagerModule();
        module.OnInitialized(container);
        module.OnInitialized(container);

        Assert.True(AppContext.TryGetSwitch("Npgsql.EnableLegacyTimestampBehavior", out var enabled) && enabled);
        DataManagerRuntime.Initialize();
    }

    /// <summary>
    /// 测试相关入口 GenericPrismModuleUsesHostOptionsAndPreservesReplacement；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewContainer、RegisterInstance、Resolve、RegisterTypes、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericPrismModuleUsesHostOptionsAndPreservesReplacement(bool reuseViews)
    {
        var container = NewContainer();
        container.RegisterInstance(new DataManagerModuleOptions { ReuseNavigationViews = reuseViews });
        var replacement = new SQLManager();
        container.RegisterInstance<ISQLManager>(replacement);
        var module = container.Resolve<DataManagerModule<TestEntity, ExcelPerson>>();
        module.RegisterTypes(container);
        var workspace = container.Resolve<SqlWorkspace<TestEntity>>();
        module.RegisterTypes(container);
        Assert.Same(replacement, container.Resolve<ISQLManager>());
        Assert.Same(workspace, container.Resolve<SqlWorkspace<TestEntity>>());
        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.SqlView)));
        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.ExcelView)));
        Assert.True(container.IsRegistered(typeof(object), nameof(DataManager.Controls.Views.LogView)));
        module.OnInitialized(container);
    }

    /// <summary>
    /// 测试相关入口 NewContainer；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static DryIocContainerExtension NewContainer() => new();
}

/// <summary>
/// 真实工作台装配验证：Workbench 以瞬时页面模式注册，视图每次解析新建、Workspace 单例常驻。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class ModuleWorkbenchCompositionTests(WorkbenchAppFixture host)
{
    private readonly WorkbenchAppFixture _host = host;

    /// <summary>
    /// 测试相关入口 PrismCatalogInitializesTypedModuleBeforeWorkbenchNavigation；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Single、Equal、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PrismCatalogInitializesTypedModuleBeforeWorkbenchNavigation()
    {
        _host.Invoke(() =>
        {
            var catalog = Prism.Ioc.ContainerLocator.Container.Resolve<IModuleCatalog>();
            var typedModule = Assert.Single(catalog.Modules, module =>
                module.ModuleType == typeof(DataManagerModule<TestEntity, ExcelPerson>).AssemblyQualifiedName);
            Assert.Equal(ModuleState.Initialized, typedModule.State);
            Assert.DoesNotContain(catalog.Modules, module =>
                module.ModuleType == typeof(DataManagerModule).AssemblyQualifiedName);
        });
    }

    /// <summary>
    /// 测试相关入口 WorkbenchComposition_TransientViews_SingletonWorkspaces；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、nameof、NotSame、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WorkbenchComposition_TransientViews_SingletonWorkspaces()
    {
        _host.Invoke(() =>
        {
            var container = Prism.Ioc.ContainerLocator.Container;
            var first = container.Resolve<object>(nameof(DataManager.Controls.Views.SqlView));
            var second = container.Resolve<object>(nameof(DataManager.Controls.Views.SqlView));
            Assert.NotSame(first, second);

            Assert.Same(
                container.Resolve<SqlWorkspace<TestEntity>>(),
                container.Resolve<SqlWorkspace<TestEntity>>());
            Assert.Same(container.Resolve<LogWorkspace>(), container.Resolve<LogWorkspace>());
        });
    }
}
