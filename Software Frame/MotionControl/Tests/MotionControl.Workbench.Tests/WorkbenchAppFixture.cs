// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchAppBootCollection、WorkbenchAppFixture。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Threading、MotionControl.Workbench、MotionControl.Controls.Services、MotionControl.Controls.Views、MotionControl.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using Workbench.TestSupport;
using System.Windows;
using System.Windows.Threading;
using MotionControl.Workbench;
using MotionControl.Controls.Services;
using MotionControl.Controls.Views;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;
using Prism.Ioc;
using Prism.Regions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MotionControl.Workbench.Tests;

/// <summary>工作台 STA 宿主：整个测试集合只启动一次 App，断言经 Invoke 封送到 UI 线程。</summary>
[CollectionDefinition("WorkbenchAppBoot")]
public sealed class WorkbenchAppBootCollection : ICollectionFixture<WorkbenchAppFixture> { }

/// <summary>STA 应用宿主夹具（WPF 单 Application 实例约束，各测各 new App 必挂）。</summary>
public sealed class WorkbenchAppFixture : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private int _disposed;
    private WorkbenchTestLifetime? _lifetime;
    private IMotionCard? _shutdownCard;
    private AxisWorkspace[] _shutdownAxes = [];

    /// <summary>
    /// 初始化 WorkbenchAppFixture 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent、BeginInvoke、ObserveMainWindow、Set、Run；具体条件和先后次序见方法体。
    /// </summary>
    public WorkbenchAppFixture()
    {
        _thread = new Thread(() =>
        {
            var app = new App();
            _lifetime = new WorkbenchTestLifetime(app);
            app.InitializeComponent(); // 等价于生产入口：先载 App.xaml 再 Run（StartupUri 拉起主窗）。
            app.Startup += (_, _) => app.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    _lifetime.ObserveMainWindow();
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
        (_lifetime ?? throw new InvalidOperationException("工作台未初始化退出处理。"))
            .CloseAndWait(_thread, StartRunningShutdownScenario);
        _ready.Dispose();
        Assert.NotNull(_shutdownCard);
        Assert.False(_shutdownCard.IsConnected);
        Assert.All(_shutdownAxes, axis =>
        {
            Assert.Equal(0, axis.RefreshSubscriberCount);
            Assert.False(axis.Model.IsConnected);
            Assert.False(axis.Model.Moving);
        });
    }

    /// <summary>Leaves actual UI jog, another moving axis and refresh subscriptions active before close.</summary>
    /// <remarks>
    /// 验证 StartRunningShutdownScenario 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Resolve、Connect、True、IsAssignableFrom、ToArray；具体条件和先后次序见方法体。
    /// </remarks>
    private void StartRunningShutdownScenario(Application app)
    {
        var container = ContainerLocator.Container;
        var workspace = container.Resolve<MotionWorkspace>();
        workspace.Model.SelectedVendor = EMotionCardType.Simulation;
        workspace.Model.AxisCount = 2;
        workspace.Connect();
        Assert.True(workspace.Model.IsConnected);
        _shutdownCard = Assert.IsAssignableFrom<IMotionCard>(workspace.Model.SelectedCard);
        _shutdownAxes = Enumerable.Range(0, _shutdownCard.AxisCount)
            .Select(axis => AxisWorkspace.For(_shutdownCard, axis)).ToArray();
        var page = Assert.IsType<MotionView>(Assert.Single(container.Resolve<IRegionManager>().Regions["MotionRegion"].ActiveViews));
        Descendants(page).OfType<TabControl>().Single().SelectedIndex = 0;
        app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.True(_shutdownAxes[0].RefreshSubscriberCount > 0);
        var jog = Assert.IsType<AxisJogControl>(page.FindName("JogControl"));
        Assert.True(jog.IsLoaded);
        Assert.True(jog.ViewModel!.SetServo(true).IsSuccess);
        var button = Descendants(jog).OfType<Button>().Single(b => Equals(b.Tag, "Positive"));
        app.MainWindow.Activate();
        button.Focus();
        app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
        });
        Assert.True(_shutdownCard.ReadAxisStatus(0).Value.Moving);
        Assert.True(_shutdownCard.ServoOnResult(1).IsSuccess);
        Assert.True(_shutdownCard.JogStartResult(1, EDirection.Positive).IsSuccess);
        Assert.True(_shutdownCard.ReadAxisStatus(1).Value.Moving);
        var initialPosition = _shutdownAxes[0].Model.CommandPosition;
        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(3);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            if (_shutdownAxes[0].Model.CommandPosition != initialPosition || DateTime.UtcNow >= deadline)
                frame.Continue = false;
        };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Assert.NotEqual(initialPosition, _shutdownAxes[0].Model.CommandPosition);
        Assert.True(_shutdownCard.ReadAxisStatus(0).Value.Moving);
        Assert.True(_shutdownCard.ReadAxisStatus(1).Value.Moving);
    }

    /// <summary>
    /// 验证 Descendants 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、Descendants；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
