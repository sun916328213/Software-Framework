// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchAppBootCollection、WorkbenchAppFixture。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Workbench、System.Windows、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using Workbench.TestSupport;
using Prism.Ioc;
using System.Windows;
using System.Windows.Threading;

namespace Vision.Workbench.Tests;

/// <summary>工作台 STA 宿主：整个测试集合只启动一次 App，断言经 Invoke 封送到 UI 线程。</summary>
[CollectionDefinition("WorkbenchAppBoot")]
public sealed class WorkbenchAppBootCollection : ICollectionFixture<WorkbenchAppFixture> { }

/// <summary>
/// STA 应用宿主夹具（WPF 单 Application 实例约束，各测各 new App 必挂）。
/// <para>
/// 走生产Prism入口：载入App.xaml、创建Shell与模块，再由OnInitialized选择默认任务页。
/// 在其他测试导航前记录实际初始视图，避免依赖用例顺序或通过事后导航假装验证默认页。
/// 全集合共享一个Application；局部关闭测试不得关闭这个主窗口，也不启动生产硬件。
/// </para>
/// </summary>
public sealed class WorkbenchAppFixture : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private int _disposed;
    private WorkbenchTestLifetime? _lifetime;
    public Type? InitialActiveViewType { get; private set; }

    /// <summary>
    /// 初始化 WorkbenchAppFixture 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent、BeginInvoke、Set、Run、SetApartmentState；具体条件和先后次序见方法体。
    /// </summary>
    public WorkbenchAppFixture()
    {
        _thread = new Thread(() =>
        {
            var app = new App();
            _lifetime = new WorkbenchTestLifetime(app);
            app.InitializeComponent(); // 等价于生产入口：先载 App.xaml（含 Vision.Controls 的 ResourceDic 合并）
            app.Startup += (_, _) => app.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    _lifetime.ObserveMainWindow();
                    var regions = Prism.Ioc.ContainerLocator.Container.Resolve<Prism.Regions.IRegionManager>();
                    InitialActiveViewType = regions.Regions[global::Vision.Workbench.ViewModels.MainWindowViewModel.MainRegion].ActiveViews.Single().GetType();
                    _ready.Set();
                }), DispatcherPriority.ApplicationIdle);
            app.Run();
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(90)))
            throw new TimeoutException("工作台启动超时");
    }

    /// <summary>在 UI 线程同步执行（异常直接抛回调用方）。</summary>
    public void Invoke(Action action) => Application.Current.Dispatcher.Invoke(action);

    /// <summary>在 UI 线程同步执行并返回值。</summary>
    public T Invoke<T>(Func<T> func) => Application.Current.Dispatcher.Invoke(func);

    /// <summary>
    /// 经共用生命周期处理关闭主窗口并等待 UI 线程退出；关闭错误和超时会使测试失败。
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        (_lifetime ?? throw new InvalidOperationException("工作台未初始化退出处理。")).CloseAndWait(_thread);
        _ready.Dispose();
    }
}
