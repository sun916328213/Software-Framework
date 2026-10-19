// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：App。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls.Workbench、System.Windows、Extension.UI.Theme、Prism.DryIoc、Prism.Ioc、Prism.Modularity。导入不代表所有类型都会在运行时加载。
using WPFCustomControls.Workbench;
using SecsGem.Workbench.Views;
using System.Windows;
using Extension.UI.Theme;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace SecsGem.Workbench;

/// <summary>Creates an empty shell; the module catalog provides services before the home page is navigated.</summary>
/// <remarks>
/// App 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：PrismApplication。具体约束按这些契约及各成员实现解释。
/// </remarks>
public partial class App : PrismApplication
{
    private WorkbenchLifetime? _lifetime;

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnStartup。
    /// 正文中的关键协作调用包括 EnsureAliasBrushes、OnStartup；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeService.EnsureAliasBrushes();
        base.OnStartup(e);
    }
    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateShell。
    /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
    /// </summary>
    protected override Window CreateShell() => Container.Resolve<MainWindow>();
    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 RegisterSingleton、Register、nameof；具体条件和先后次序见方法体。
    /// </summary>
    protected override void RegisterTypes(IContainerRegistry registry)
    {
        registry.RegisterSingleton<ThemeService>();
        registry.RegisterSingleton<Services.RecipeTestWorkspace>();
        registry.Register(typeof(object), typeof(RecipeTestView), nameof(RecipeTestView));
    }
    /// <summary>
    /// 配置当前对象或组合入口；对应入口为 ConfigureModuleCatalog。
    /// 正文中的关键协作调用包括 AddModule；具体条件和先后次序见方法体。
    /// </summary>
    protected override void ConfigureModuleCatalog(IModuleCatalog catalog)
    {
        catalog.AddModule<DataManager.Controls.DataManagerModule>();
        catalog.AddModule<SecsGem.Controls.SecsGemModule>();
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// 正文中的关键协作调用包括 Initialize、Resolve、OnInitialized、CloseRegionAsync、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnInitialized()
    {
        Container.Resolve<ThemeService>().Initialize();
        base.OnInitialized();

        // Secs4NetTransport 的收发/链路日志按名字取 "SEMILog"（Secs4NetTransport.cs:35）。
        // 不注册的话那个索引返回 null，所有 log?.Add 全部静默丢弃 —— 现场排查"报文收不到"时
        // 一行痕迹都没有，等于瞎猜。这里建一个落到工作台目录下的文件日志。
        var logManager = Container.Resolve<DataManager.Runtime.Log.LogManager>();
        if (logManager.TryGet("SEMILog", out var existing) && existing != null)
        {
            // 已注册则不重复创建
        }
        else
        {
            try
            {
                logManager.Create(new DataManager.Core.Log.Definition.LogParam
                {
                    Name = "SEMILog",
                    FilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Config", "Log", "SEMILog.txt"),
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"SEMILog 创建失败（收发日志将不可见）: {ex.Message}");
            }
        }

        // 原 SecsWorkbenchView 构造函数里的两件事：独立工作台才启用 WPF 终端显示和示例 HCACK 远程命令处理器，
        // 通用 Controls 页面不替换宿主的远程命令处理器。
        var runtime = Container.Resolve<SecsGem.Runtime.SecsGemRuntime>();
        runtime.TerminalDisplay = new SecsGem.Controls.Terminal.WpfTerminalDisplay();
        // 载入 Config\SecsJson 的 VID/ReportID/CEID，并绑定 StatusManager 的四个名字。
        // 必须在任何 Connect/上下线之前：S6F11 组包、Host 侧 S1F17 判定、控制状态上报全靠这张表，
        // 表空时这些路径静默跳过而不报错。工作台源码不带 Config 目录，走 DataBase.LoadDefaults 的打底项。
        try { runtime.Config(); }
        catch (Exception ex) { Console.Error.WriteLine($"GEM 配置加载失败（继续启动）：{ex.Message}"); }
        var workspace = Container.Resolve<SecsGem.Controls.Services.SecsWorkspace>();
        workspace.EnableWorkbenchCommandHandler();
        var recipes = Container.Resolve<Services.RecipeTestWorkspace>().Model;
        workspace.ConfigureRecipeSource(recipes.Recipes, () => recipes.CurrentPpid, recipes.NotifyRecipesChanged,
            recipes.NotifyRecipeReceived, recipes.NotifyRecipesDeleted, recipes.NotifyFormattedRecipeReceived,
            formattedCodec: new SecsGem.Controls.Services.ParameterBlockRecipeCodec<Models.WorkbenchRecipe>(
                Models.WorkbenchRecipe.FormattedParameterNames, model: "WORKBENCH", softwareRevision: "1.0"));
        // 独立模拟宿主：启动时把示例写入同一个协议内存库，使 Host 的查询/读取立即可用。
        workspace.ImportHostRecipes();
        _lifetime = new WorkbenchLifetime(MainWindow,
            () => { (MainWindow.DataContext as IDisposable)?.Dispose(); return ValueTask.CompletedTask; },
            () => WorkbenchLifetime.CloseRegionAsync(Container.Resolve<IRegionManager>(), ViewModels.MainWindowViewModel.MainRegion),
            // 关闭前先下线：控制状态转 OFFLINE + S6F11 通知 Host，再断连接，最后才释放运行时
            async () =>
            {
                var runtime = Container.Resolve<SecsGem.Runtime.SecsGemRuntime>();
                Container.Resolve<SecsGem.Controls.Services.SecsWorkspace>().Dispose();
                runtime.SetEventHandler(null);
                try { await runtime.ShutdownAsync(TimeSpan.FromSeconds(5)); }
                finally { runtime.Dispose(); }
            });
        (MainWindow?.DataContext as ViewModels.MainWindowViewModel)?.SelectFirst();
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnExit。
    /// 正文中的关键协作调用包括 OnExit；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _ = _lifetime?.BeginCleanup();
        base.OnExit(e);
    }
}
