// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ProductionCollection、ProductionFixture、HostCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.IO、System.Windows.Controls、System.Windows.Media、System.Windows.Media.Imaging、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Device.Core.Cameras;
using MachineApp;
using MachineApp.Infra.Device;
using MachineApp.UI.View;
using MachineApp.UI.ViewModel;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using SecsGem.Core;
using Vision.Controls.View;
using Vision.Tools.Halcon.Sessions;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// ProductionCollection 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：ICollectionFixture&lt;ProductionFixture&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
[CollectionDefinition("ProductionComposition", DisableParallelization = true)]
public sealed class ProductionCollection : ICollectionFixture<ProductionFixture>;

/// <summary>Uses the production App/XAML/catalog/shell, replacing only hardware startup execution.</summary>
/// <remarks>
/// ProductionFixture 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class ProductionFixture : IDisposable
{
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private Exception? _fault;
    private App? _app;
    /// <summary>
    /// 初始化 ProductionFixture 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent、BeginInvoke、Run、Set、SetApartmentState；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public ProductionFixture()
    {
        _thread = new Thread(() =>
        {
            try
            {
                _app = new App(executeProductionStartup: false);
                _app.InitializeComponent();
                _app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _app.Startup += (_, _) => _app.Dispatcher.BeginInvoke(new Action(_ready.Set), DispatcherPriority.ApplicationIdle);
                _app.Run();
            }
            catch (Exception ex) { _fault = ex; _ready.Set(); }
        }) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(60))) throw new TimeoutException("整机模块装配超时。");
        if (_fault is not null) throw new InvalidOperationException("整机模块装配失败。", _fault);
    }
    /// <summary>
    /// 测试相关入口 Invoke；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、action；具体条件和先后次序见方法体。
    /// </summary>
    public void Invoke(Action<IContainerProvider> action) => _app!.Dispatcher.Invoke(() => action(_app.Container));
    /// <summary>
    /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 InvokeShutdown、Join、FromSeconds、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        _app?.Dispatcher.InvokeShutdown();
        _thread.Join(TimeSpan.FromSeconds(15));
        _ready.Dispose();
    }
}

/// <summary>
/// HostCompositionTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("ProductionComposition")]
public sealed class HostCompositionTests(ProductionFixture fixture)
{
    /// <summary>
    /// 测试相关入口 FlowEditorHonorsHostNavigationPolicy；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、RegisterInstance、Register、RegisterTypes、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FlowEditorHonorsHostNavigationPolicy(bool reuse)
    {
        fixture.Invoke(_ =>
        {
            var container = new Prism.DryIoc.DryIocContainerExtension();
            container.RegisterInstance(new FlowEditor.Controls.FlowEditorModuleOptions { ReuseNavigationViews = reuse });
            container.Register<FlowEditor.Controls.FlowEditorModule>();
            container.Resolve<FlowEditor.Controls.FlowEditorModule>().RegisterTypes(container);
            var first = container.Resolve<object>("FlowChartContainerView");
            var second = container.Resolve<object>("FlowChartContainerView");
            if (reuse) Assert.Same(first, second);
            else Assert.NotSame(first, second);
            Assert.NotSame(container.Resolve<object>("FlowChartView"), container.Resolve<object>("FlowChartView"));
        });
    }

    /// <summary>
    /// 测试相关入口 FeatureControlsResolveWithIndependentBindingsAndReusableOverview；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsAssignableFrom、Resolve、NotSame、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FeatureControlsResolveWithIndependentBindingsAndReusableOverview()
    {
        fixture.Invoke(container =>
        {
            foreach (var name in new[] { "MotionOverviewControl", "AxisStatusControl", "AxisJogControl",
                         "AxisOperationControl", "MotionView", "ToolEditorView", "ToolCardView", "RoiEditorView", "FlowChartView" })
            {
                Assert.IsAssignableFrom<UserControl>(container.Resolve<object>(name));
                Assert.NotSame(container.Resolve<object>(name), container.Resolve<object>(name));
            }
            Assert.Same(container.Resolve<object>("SecsOverviewView"), container.Resolve<object>("SecsOverviewView"));
            Assert.Empty(container.Resolve<MotionControl.Runtime.Services.MotionManager>().Devices);
        });
    }

    /// <summary>
    /// 测试相关入口 RepeatedProductionModuleRegistrationKeepsPrewarmedPages；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、ToDictionary、Resolve、RegisterTypes、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RepeatedProductionModuleRegistrationKeepsPrewarmedPages()
    {
        fixture.Invoke(container =>
        {
            var before = new[] { "LogView", "CameraView" }
                .ToDictionary(name => name, name => container.Resolve<object>(name));
            var registry = (IContainerRegistry)container;
            new DataManager.Controls.DataManagerModule<MachineApp.Infra.Persistence.Entities.SqlConfigEntity, MachineApp.Infra.Persistence.Entities.ExcelConfigEntity>(
                new DataManager.Controls.DataManagerModuleOptions { ReuseNavigationViews = true }).RegisterTypes(registry);
            new Device.Controls.DeviceControlsModule().RegisterTypes(registry);
            foreach (var (name, view) in before)
                Assert.Same(view, container.Resolve<object>(name));
        });
    }

    /// <summary>
    /// 测试相关入口 PublicControlsNavigateToThePrewarmedInstance；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、IsType、Equal、GetName；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PublicControlsNavigateToThePrewarmedInstance()
    {
        fixture.Invoke(container =>
        {
            const string name = "PaginationView";
            var prewarmed = container.Resolve<object>(name);
            Assert.IsType<WPFControls.Controls.Views.PaginationView>(prewarmed);
            Assert.Equal("WPFControls", prewarmed.GetType().Assembly.GetName().Name);
            new WPFControls.WPFControlsModule().RegisterTypes((IContainerRegistry)container);
            Assert.Same(prewarmed, container.Resolve<object>(name));
            var manager = container.Resolve<IRegionManager>();
            var region = new Region();
            manager.Regions.Add("PublicControlsAcceptance", region);
            NavigationResult? result = null;
            manager.RequestNavigate("PublicControlsAcceptance", name, navigation => result = navigation);
            Assert.True(result?.Result, result?.Error?.ToString());
            Assert.Same(prewarmed, Assert.Single(region.ActiveViews));
            var view = (FrameworkElement)prewarmed;
            view.Measure(new Size(500, 100));
            view.Arrange(new Rect(0, 0, 500, 100));
            view.UpdateLayout();
            new RenderTargetBitmap(500, 100, 96, 96, PixelFormats.Pbgra32).Render(view);
        });
    }

    /// <summary>
    /// 测试相关入口 ReinitializingUiModulesDoesNotDuplicateResourcesOrWarmup；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、OnInitialized、Equal、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ReinitializingUiModulesDoesNotDuplicateResourcesOrWarmup()
    {
        fixture.Invoke(container =>
        {
            var resources = Application.Current.Resources.MergedDictionaries;
            var before = resources.Count;
            var warmup = ScriptEditor.Controls.ScriptEditorModule.WarmupTask;
            new WPFControls.WPFControlsModule().OnInitialized(container);
            new WPFControls.WPFControlsModule().OnInitialized(container);
            new FlowEditor.Controls.FlowEditorModule().OnInitialized(container);
            new ScriptEditor.Controls.ScriptEditorModule().OnInitialized(container);
            Assert.Equal(before, resources.Count);
            Assert.Same(warmup, ScriptEditor.Controls.ScriptEditorModule.WarmupTask);
        });
    }

    /// <summary>
    /// 测试相关入口 ProductionCatalogLoadsEveryModuleThroughPrism；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、ToArray、Resolve、Equal、All；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProductionCatalogLoadsEveryModuleThroughPrism()
    {
        fixture.Invoke(container =>
        {
            var modules = container.Resolve<IModuleCatalog>().Modules.ToArray();
            Assert.Equal(10, modules.Length);
            Assert.All(modules, module => Assert.Equal(ModuleState.Initialized, module.State));
            Assert.Contains(modules, module => module.ModuleName.StartsWith("DataManagerModule", StringComparison.Ordinal));
            Assert.Contains(modules, module => module.ModuleName == "DeviceControlsModule");
            Assert.Contains(modules, module => module.ModuleName == "CommunicationModule");
            Assert.Contains(modules, module => module.ModuleName == "MotionControlsModule");
            Assert.Contains(modules, module => module.ModuleName == "VisionControlsModule");
            Assert.Contains(modules, module => module.ModuleName == "SecsGemModule");
        });
    }
    /// <summary>
    /// 测试相关入口 CompositionUsesNewFactoriesWithoutConnectingProductionDevices；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType、Resolve、Empty、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CompositionUsesNewFactoriesWithoutConnectingProductionDevices()
    {
        fixture.Invoke(container =>
        {
            Assert.IsType<Device.Runtime.Cameras.CameraFactory>(container.Resolve<ICameraFactory>());
            var session = container.Resolve<ProductionDeviceSession>();
            Assert.Empty(session.Cameras);
            Assert.Empty(session.Lights);
            Assert.Empty(session.Rfid);
            Assert.Empty(session.Scanners);
            Assert.False(container.Resolve<ISecsGemRuntime>().GetState().IsConnected);
        });
    }
    /// <summary>
    /// 测试相关入口 ProductionVisionPageRendersTheNewEditorAndTypedSessions；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType、Resolve、nameof、Measure；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProductionVisionPageRendersTheNewEditorAndTypedSessions()
    {
        fixture.Invoke(container =>
        {
            var view = Assert.IsType<VisionView>(container.Resolve<object>(nameof(VisionView)));
            var model = Assert.IsType<VisionViewModel>(view.DataContext);
            Assert.IsType<BarcodeSession>(model.Datas.BarcodeReader);
            Assert.IsType<ShapeMatchSession>(model.Datas.TemplateMatch_Accessory);
            view.Measure(new Size(900, 700));
            view.Arrange(new Rect(0, 0, 900, 700));
            view.UpdateLayout();
            Assert.NotEmpty(Descendants(view).OfType<ToolEditorView>());
            var bitmap = new RenderTargetBitmap(900, 700, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            Assert.Equal(900, bitmap.PixelWidth);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(AppContext.BaseDirectory, "vision-page.png"));
            encoder.Save(output);
        });
    }
    /// <summary>
    /// 测试相关入口 RealRegionNavigationLoadsVisionAndNewCameraPages；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Add、nameof、RequestNavigate；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RealRegionNavigationLoadsVisionAndNewCameraPages()
    {
        fixture.Invoke(container =>
        {
            var manager = container.Resolve<IRegionManager>();
            var region = new Region();
            manager.Regions.Add("MigrationAcceptanceRegion", region);
            foreach (var name in new[] { nameof(VisionView), "CamManagerView" })
            {
                NavigationResult? result = null;
                manager.RequestNavigate("MigrationAcceptanceRegion", name, navigation => result = navigation);
                Assert.NotNull(result);
                Assert.True(result!.Result, result.Error?.ToString());
                Assert.NotEmpty(region.ActiveViews.OfType<FrameworkElement>());
            }
        });
    }
    /// <summary>
    /// 测试相关入口 Descendants；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、Descendants；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
