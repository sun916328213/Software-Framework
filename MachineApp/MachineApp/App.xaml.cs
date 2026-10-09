// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：App。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、MachineApp.Infra.Adapters、MachineApp.Infra.Persistence.Entities、MachineApp.Infra.Persistence.Mapping、LiveChartsCore.SkiaSharpView、System.Globalization。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using MachineApp.Infra.Adapters;
using MachineApp.Infra.Persistence.Entities;
using MachineApp.Infra.Persistence.Mapping;
using LiveChartsCore.SkiaSharpView;
using System.Globalization;
using WPFControls.Integration.Device;
using SecsGem.Controls;
using Device.Core.LabelPrinting;
using Device.Core.Scanners;
using Device.Core.Rfid;
using Device.Core.Lights;
using Device.Core.Cameras;
using DataManager.Controls;
using MachineApp.Infra.Device;
using Extension.Integration.Prism;
using Extension.Integration.Mapster;
using FlowEditor.Controls;
using MachineApp.Business.Flow;
using MachineApp.Business.Station;
using MachineApp.Infra.Core;
using MachineApp.Infra.Tool;
using MachineApp.UI.Model;
using MachineApp.UI.View;
// 添加ViewModel命名空间
using MachineApp.UI.ViewModel;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using ScriptEditor.Controls;
using ScriptEditor.Controls.Services;
using ScriptEditor.Runtime.Services;
using SecsGem.Runtime;
using System.Windows;
using System.Windows.Threading;
using WPFControls;
using WPFControls.ProgramControl.Services;
using WPFControls.ProgramControl.ViewModels;
using WPFControls.ProgramControl.Views;
using WPFCustomControls;

namespace MachineApp
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {
        private readonly bool _executeProductionStartup;
        /// <summary>
        /// 初始化 App 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public App() : this(true) { }
        /// <summary>
        /// 初始化 App 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        internal App(bool executeProductionStartup) => _executeProductionStartup = executeProductionStartup;
        // 主内容区域菜单导航页面清单（按导航名，与 Init_Menu 中 RequestNavigate 的名称对应）。
        private static readonly string[] NavigationViewNames =
        {
            "Home",
            "Recipe",
            "PrintManagerView",
            "DisplayView",
            "AutoFlowView",
            "InitializeFlowView",
            "StartupFlowView",
            "ShutdownFlowView",
            "CamManagerView",
            "VisionView",
            "RFIDManagerView",
            "ResourceMonitorView",
            "LogQueryView",
            "SEMILogQueryView",
            "DataQueryView",
            "ImageQueryView",
            "CapacityStatisticsView",
            "AlarmInfoQueryView",
            "UserInfoManagerView",
            "SEMIManagerView",
            "AlarmInfoManagerView",
            "LogConfigView"
        };

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateShell。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        /// <summary>
        /// 预热主内容区域导航页面：按导航名构造视图 + 绑定 ViewModel（与 Prism
        /// RegionNavigationContentLoader 相同的 object+导航名 解析路径 + AutoWireViewModel 绑定）。
        /// 视图已注册为 (object, 导航名) 单例（见 RegisterTypes 及各模块 RegisterNavigationView），
        /// 因此这里 Resolve 出的实例就是导航首次进入时解析到的同一个实例；再强制
        /// Measure/Arrange/UpdateLayout + 离屏 RenderTargetBitmap 真实渲染，
        /// 一次性支付首次显示时的模板实例化、布局与绘制开销（含 EditTabControl 选中页内容、
        /// FlowChartView 的节点/连线渲染等深层视觉树）。
        /// ViewModel 在此一并解析并赋给 DataContext，导航时已绑定则自动跳过 VM 创建。
        /// 注意：Resolve + VM 绑定 + 布局必须发生在 UI 线程（视图是 DispatcherObject），
        /// 因此在加载窗口的阻塞流程中直接执行，不能放入后台线程。
        /// </summary>
        private void PrewarmNavigationViews()
        {
            foreach (var name in NavigationViewNames.Distinct())
            {
                try
                {
                    if (Container.Resolve(typeof(object), name) is FrameworkElement view)
                    {
                        ForceFullRender(view);
                    }
                }
                catch (Exception ex)
                {
                    // 2026-09-23：原实现未把 ex 作为 innerException 传出，预热失败时看不到真因
                    // （见 WPFControls-架构分析与优化方向.md 3.8）。同时带上视图名，便于定位。
                    throw new Exception($"View预热失败：视图「{name}」", ex);
                }
            }
        }

        /// <summary>
        /// 以固定尺寸强制 Measure/Arrange/UpdateLayout，并用 RenderTargetBitmap 执行一次
        /// 真实离屏渲染。未挂接到窗口的视图仅 UpdateLayout 不会实例化深层模板（TabControl 内容、
        /// ItemsControl 容器、FlowChart 节点连线），渲染也不执行；必须用显式尺寸测量 + 离屏渲染，
        /// 才能在启动阶段真正支付首次显示的全部成本。
        /// </summary>
        private static void ForceFullRender(FrameworkElement view)
        {
            var size = new Size(900, 700);
            view.Measure(size);
            view.Arrange(new Rect(size));
            view.UpdateLayout();
        }



        /// <summary>
        /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
        /// 正文中的关键协作调用包括 RegisterServices、RegisterSingleton、RegisterCapacity、RegisterInstance、RegisterNavigationView；具体条件和先后次序见方法体。
        /// </summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            WPFControlsComposition.RegisterServices(containerRegistry);
            containerRegistry.RegisterSingleton<FlowNodes.Device.ICameraProvider, ProductionFlowCameraProvider>();
            containerRegistry.RegisterSingleton<MainWindow>();
            // T11：先按类型暴露 ISQL<CSQLData> / ISQL<CapacityStatisticsInfo>，下面的产能端口
            // 与通用查询页（DataQueryView）都经它取实例，不再依赖 SQLManager 的注册序号。
            MachineApp.Infra.Persistence.SqlInstanceComposition.Register(containerRegistry);
            MachineApp.Infra.Persistence.PackingPersistenceComposition.RegisterCapacity(containerRegistry);






            // 仅提供生产导航策略；服务和页面由泛型 Prism 模块自动注册。
            containerRegistry.RegisterInstance(new DataManagerModuleOptions { ReuseNavigationViews = true });

            containerRegistry.RegisterNavigationView<Home>();
            containerRegistry.RegisterNavigationView<UI.View.Recipe, RecipeViewModel>();

            containerRegistry.RegisterNavigationView<VisionView, VisionViewModel>();
            containerRegistry.RegisterNavigationView<DataQueryView, DataQueryViewModel<CSQLData, CSQLDataDTO>>();


            // Map business ports to the same production instances; factories retain deferred lookup.
            containerRegistry.RegisterSingleton<IProductionPlc>(provider => provider.Resolve<PLCTool>());
            containerRegistry.RegisterSingleton<IProductionSecs>(provider => provider.Resolve<SecsTool>());
            containerRegistry.RegisterSingleton<IProductionVision>(provider => provider.Resolve<VisionTool>());
            containerRegistry.RegisterSingleton<IProductionDevices>(provider => provider.Resolve<ProductionDeviceSession>());
            containerRegistry.RegisterSingleton<IProductionLog>(provider => new ProductionLogAdapter(provider.Resolve<LogInfoService>()));
            containerRegistry.RegisterSingleton<IProductionPrinter>(provider => new ProductionPrinterAdapter(provider.Resolve<PrintManagerService>()));
            containerRegistry.Register<StationServices>(provider => new StationServices(
                () => provider.Resolve<IProductionLog>(),
                () => provider.Resolve<IProductionSecs>(),
                () => provider.Resolve<IProductionVision>(),
                () => provider.Resolve<IProductionDevices>(),
                () => provider.Resolve<IProductionPrinter>(),
                () => provider.Resolve<IProductionPlc>()));
            containerRegistry.RegisterSingleton<AutoProcess>();
            containerRegistry.RegisterSingleton<ProductionDeviceSession>();
            // P5：PLC 最小可用恢复。点位表非法时 PlcTagCatalog 构造即抛错（启动即失败）；
            // PLC.json 缺失时使用默认连接配置，连接阶段再反映真实状态。
            var plcConfigDir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Config");
            containerRegistry.RegisterInstance(
                MachineApp.Infra.Plc.PlcOptions.Load(System.IO.Path.Combine(plcConfigDir, "PLC.json")));
            containerRegistry.RegisterInstance(
                new MachineApp.Infra.Plc.PlcTagCatalog(System.IO.Path.Combine(plcConfigDir, "PLCIO.csv")));
            containerRegistry.RegisterSingleton<MachineApp.Infra.Plc.PlcService>();
            containerRegistry.RegisterSingleton<PLCTool>();
            containerRegistry.RegisterSingleton<VisionTool>();
            containerRegistry.RegisterSingleton<VisionViewModel>();
            containerRegistry.RegisterSingleton<DataQueryService<CSQLData, CSQLDataDTO>>();

            containerRegistry.RegisterSingleton<Load>();
            containerRegistry.RegisterSingleton<ReadID>();
            containerRegistry.RegisterSingleton<Accessory>();
            containerRegistry.RegisterSingleton<Label>();
            containerRegistry.RegisterSingleton<HeatSeal>();
            containerRegistry.RegisterSingleton<UnLoad>();
            containerRegistry.RegisterSingleton<ALDetect>();
            containerRegistry.RegisterSingleton<HumidityCard>();
            containerRegistry.RegisterSingleton<Business.Flow.Recipe>();

            containerRegistry.RegisterNavigationView<Test>();
        }

        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 ConfigureModuleCatalog。
        /// 正文中的关键协作调用包括 ConfigureModules；具体条件和先后次序见方法体。
        /// </summary>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            WPFControlsComposition.ConfigureModules<SqlConfigEntity, ExcelConfigEntity>(moduleCatalog);
        }


        private static Mutex mutex = null;
        private const string APP_MUTEX_NAME = "PackingMachine";

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnStartup。
        /// 正文中的关键协作调用包括 Show、Shutdown、OnStartup；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 创建互斥体，防止多开
            bool createdNew;
            mutex = new Mutex(true, APP_MUTEX_NAME, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("程序已在运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return;
            }
            base.OnStartup(e);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnExit。
        /// 正文中的关键协作调用包括 Resolve、WriteLine、CheckAccess、ReleaseMutexResources、Invoke；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            // P5：先停 PLC 心跳并断开，释放 COM 句柄；不能只依赖进程强制退出。
            try
            {
                Container.Resolve<PLCTool>()?.StopRefresh();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"退出时停止 PLC 失败: {ex.Message}");
            }

            // 确保在应用程序主线程上释放资源
            if (this.Dispatcher.CheckAccess())
            {
                ReleaseMutexResources();
            }
            else
            {
                // 在UI线程上执行清理
                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(ReleaseMutexResources));
            }

            base.OnExit(e);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReleaseMutexResources。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private void ReleaseMutexResources()
        {
            try
            {
                mutex?.Dispose(); // 使用Dispose替代ReleaseMutex + Close
                mutex = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"释放Mutex资源时出错: {ex.Message}");
            }
        }

        // 生产环境策略由整机宿主执行，公共 UI 模块不迁移文件或改变默认文化。
        private void InitializeProductionEnvironment()
        {
            InitializeLanguage();
            if (Extension.UI.Controls.LangSwitchEx.Language == Extension.UI.Controls.ELang.ZH)
                LiveChartsCore.LiveCharts.Configure(config => config.HasGlobalSKTypeface(SkiaSharp.SKFontManager.Default.MatchCharacter('汉')));
            TryImportLegacyDeviceConfig();
        }

        /// <summary>
        /// 执行旧 Device 配置迁移（P8-3），并把结果写到调试输出。
        /// 迁移本身幂等且非破坏（新文件已存在即跳过、旧文件永不改动），
        /// 因此任何异常都不应阻断模块初始化 —— 只记日志。
        /// </summary>
        private static void TryImportLegacyDeviceConfig()
        {
            try
            {
                var result = LegacyDeviceConfigImporter.Import();

                foreach (var file in result.Imported)
                    System.Diagnostics.Debug.WriteLine($"旧设备配置已迁移：Config/{file}");
                foreach (var file in result.Skipped)
                    System.Diagnostics.Debug.WriteLine($"旧设备配置跳过（新文件已存在）：Config/{file}");
                foreach (var warning in result.Warnings)
                    System.Diagnostics.Debug.WriteLine($"旧设备配置迁移告警：{warning}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"旧设备配置迁移失败（已忽略，不影响启动）：{ex.Message}");
            }
        }

        /// <summary>
        /// 初始化应用程序语言设置
        /// </summary>
        private void InitializeLanguage()
        {
            try
            {
                // 设置默认语言为系统语言
                var culture = CultureInfo.CurrentUICulture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化语言设置时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
        /// 正文中的关键协作调用包括 InitializeProductionEnvironment、Initialize、StartOnOwnThread、InitializeAllMappings、Configure；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        protected override async void OnInitialized()
        {
            if (_executeProductionStartup) InitializeProductionEnvironment();
            if (!_executeProductionStartup)
            {
                ContainerManager.Initialize(Container);
                return;
            }
            var loading = WPFCustomControls.Window.Views.ProgramLoading.StartOnOwnThread();

            // 各初始化步骤的失败汇总（try 外声明：finally 之后仍需读取）
            var initErrors = new System.Collections.Generic.List<string>();
            StartupFlowService? startupFlowService = null;

            try
            {
                ContainerManager.Initialize(Container);
                MappingConfig.InitializeAllMappings();
                // 初始化映射配置
                CSQLDataMapping.Configure(Mapster.TypeAdapterConfig.GlobalSettings);

                // 先在后台线程解析启动/关机流程（不碰 UI）
                startupFlowService = Container.Resolve<StartupFlowService>();
                await startupFlowService.LoadFromDefaultPathAsync();

                var shutdownFlowService = Container.Resolve<ShutdownFlowService>();
                await shutdownFlowService.LoadFromDefaultPathAsync();

                // 把各初始化步骤封装为 (标题, 工作) 列表。每步工作由 ProgramLoading.ShowAsync
                // 统一投递到主 UI 线程执行，loading 线程只负责进度更新与动画，因此这里全是纯业务代码。
                // 单步异常就地捕获汇总：不中断后续步骤、不阻塞主窗口显示、真实原因可见可查。
                var items = startupFlowService.ScriptManager.Items;
                var initActions = new System.Collections.Generic.List<(string Title, System.Func<Task> Work)>();

                void RunStep(string title, System.Func<Task> work)
                {
                    initActions.Add((title, async () =>
                    {
                        try { await work(); }
                        catch (Exception ex)
                        {
                            // NRE 等异常的 Message 往往没有信息量，附加堆栈前几帧帮助定位
                            var stack = ex.StackTrace?.Split('\n')
                                .Take(3).Select(l => l.Trim()).Aggregate((a, b) => a + "\n  " + b);
                            initErrors.Add($"[{title}] {ex.GetType().Name}: {ex.Message}"
                                + (stack == null ? "" : $"\n  at {stack}"));
                            System.Diagnostics.Debug.WriteLine($"[初始化失败] {title}: {ex}");
                        }
                    }
                    ));
                }

                foreach (ScriptEditor.Core.Models.ScriptItem item in items)
                {
                    var scriptItem = item;
                    RunStep(scriptItem.Title, async () =>
                    {
                        // P3：运行状态由会话独占持有。启动脚本与编辑器共用同一会话
                        // （同一 ScriptItem → 同一 ScriptSession），因此这里执行的输出/状态
                        // 在用户打开编辑器时依然可见；不再通过 ScriptItem 上的状态镜像传递。
                        var session = ScriptEditor.Runtime.Services.RoslynScriptRuntime.Shared.GetSession(scriptItem);
                        var succeeded = await session.RunAsync();
                        if (!succeeded)
                        {
                            throw new InvalidOperationException(
                                $"脚本执行失败：{session.StatusText}{Environment.NewLine}{session.Output}");
                        }
                    });
                }

                RunStep("预热脚本编辑器", async () =>
                {
                    var scriptEditorService = Container.Resolve<ScriptEditorService>();
                    await scriptEditorService.WarmupAsync(Container);
                });

                RunStep("预热流程图", async () =>
                {
                    // 预热已注册的统一节点目录；来源由宿主组合入口指定。
                    Container.Resolve<FlowEditor.Controls.Interfaces.IFlowToolDiscoveryService>().Discover();
                    // 加载流程组（.flows 多流程容器），而非单个流程（.flow）
                    var initializeFlowService = Container.Resolve<InitializeFlowService>();
                    await initializeFlowService.LoadFlowGroupFromAsync();
                    var autoFlowService = Container.Resolve<AutoFlowService>();
                    await autoFlowService.LoadFlowGroupFromAsync();
                });

                RunStep("初始化预热", async () =>
                {
                    // 视图预热须在主 UI 线程同步执行（DispatcherObject + 真实布局/渲染），
                    // ShowAsync 已保证本步骤运行在主线程，直接调用即可。
                    PrewarmNavigationViews();
                });

                // await 到窗口关闭后再显示主窗口
                await loading.RunAsync(initActions);
            }
            finally
            {
                // 任何异常也要确保关闭 loading 窗口、结束其线程
                loading.CloseOnOwnThread();
            }


            var mainWindow = Container.Resolve<MainWindow>();
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.Focus();

            // 导航到状态栏视图
            var regionManager = Container.Resolve<IRegionManager>();
            regionManager.RequestNavigate("WindowMenuRegion", "WindowMenuView");
            regionManager.RequestNavigate("StatusBarRegion", "StatusBarView");
            regionManager.RequestNavigate("StatisticsRegion", "ResourceMonitorView"); //StatisticsView
            regionManager.RequestNavigate("MenuRegion", "MenuView");
            regionManager.RequestNavigate("ControlRegion", "ControlView");
            regionManager.RequestNavigate("IconRegion", "IconView");

            // 初始化步骤失败汇总（含脚本源码目录提示，可用 VSCode 打开排查）
            if (initErrors.Count > 0 && startupFlowService != null)
            {
                var scriptFolder = ScriptEditor.Controls.Services.ScriptManagerService.GetScriptFolder(
                    startupFlowService.DefaultFilePath);
                var summary =
                    string.Join(Environment.NewLine, initErrors)
                    + Environment.NewLine + Environment.NewLine
                    + $"各启动脚本已导出为独立源码文件（NN_标题.cs），可用 VSCode 打开编辑：{Environment.NewLine}{scriptFolder}"
                    + Environment.NewLine + "修改并保存后，重新加载启动流程即自动回填。"
                    + Environment.NewLine + $"改错时可从同名 .cs.bak 备份恢复（VSCode 已配置 .bak 以 C# 高亮打开）。";

                System.Diagnostics.Debug.WriteLine($"[启动初始化异常汇总]{Environment.NewLine}{summary}");
                MessageBox.Show(summary, "启动初始化存在失败步骤",
                    MessageBoxButton.OK, MessageBoxImage.Warning);


            }

            //Init init = new Init();
            //init.InitControl();
        }
    }
}
