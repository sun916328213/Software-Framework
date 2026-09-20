using Communication;
using DataManager;
using Device;
using Extension.Integration.Prism;
using Extension.Integration.Mapster;
using FlowEditor;
using Packing_Machine.Business.Flow;
using Packing_Machine.Business.Station;
using Packing_Machine.Infra.Core;
using Packing_Machine.Infra.Tool;
using Packing_Machine.UI.Model;
using Packing_Machine.UI.View;
// 添加ViewModel命名空间
using Packing_Machine.UI.ViewModel;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using ScriptEditor;
using ScriptEditor.Services;
using Secs;
using System.Windows;
using System.Windows.Threading;
using WPFControl;
using WPFControl.ProgramControl.Services;
using WPFControl.ProgramControl.ViewModels;
using WPFControl.ProgramControl.Views;
using WPFCustomControls;

namespace Packing_Machine
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {
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
                    throw new Exception("View预热失败");
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



        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<MainWindow>();

            containerRegistry.RegisterNavigationView<Home>();
            containerRegistry.RegisterNavigationView<UI.View.Recipe, RecipeViewModel>();
 
            containerRegistry.RegisterNavigationView<VisionView, VisionViewModel>();
            containerRegistry.RegisterNavigationView<DataQueryView, DataQueryViewModel<CSQLData, CSQLDataDTO>>();

         
            containerRegistry.RegisterSingleton<AutoProcess>();
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

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // 添加WPFControl模块到模块目录
            moduleCatalog.AddModule<DataManagerModule>();
            moduleCatalog.AddModule<CommunicationModule>();
            moduleCatalog.AddModule<SecsToolModule>();
            moduleCatalog.AddModule<DeviceModule>();
            moduleCatalog.AddModule<WPFControlModule>();
            moduleCatalog.AddModule<WPFCustomControlsModule>();
            moduleCatalog.AddModule<FlowEditorModule>();
            moduleCatalog.AddModule<ScriptEditorModule>();
        }


        private static Mutex mutex = null;
        private const string APP_MUTEX_NAME = "PackingMachine";

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

        protected override void OnExit(ExitEventArgs e)
        {
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

        protected override async void OnInitialized()
        {
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

                foreach (ScriptEditor.Models.ScriptItem item in items)
                {
                    var scriptItem = item;
                    RunStep(scriptItem.Title, async () => await scriptItem.ExecuteAsync());
                }

                RunStep("预热脚本编辑器", async () =>
                {
                    var scriptEditorService = Container.Resolve<ScriptEditorService>();
                    await scriptEditorService.WarmupAsync(Container);
                });

                RunStep("预热流程图", async () =>
                {
                    // 强制加载 FlowNodes 程序集，确保其节点能被工具箱扫描到
                    System.Reflection.Assembly.Load("FlowNodes");
                    Container.Resolve<FlowEditor.Interfaces.IFlowToolDiscoveryService>().Discover();
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
                var scriptFolder = ScriptEditor.Services.ScriptManagerService.GetScriptFolder(
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