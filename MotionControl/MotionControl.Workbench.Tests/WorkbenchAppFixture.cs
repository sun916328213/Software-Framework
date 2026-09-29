using System.Windows;
using System.Windows.Threading;
using MotionControl.Workbench;

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

    public WorkbenchAppFixture()
    {
        _thread = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent(); // 等价于生产入口：先载 App.xaml 再 Run（StartupUri 拉起主窗）。
            app.Startup += (_, _) => app.Dispatcher.BeginInvoke(
                new Action(() => _ready.Set()), DispatcherPriority.ApplicationIdle);
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            if (Application.Current is not null)
                Application.Current.Dispatcher.InvokeShutdown();
        }
        catch
        {
            // 关停期异常不掩盖测试结果。
        }
        _thread.Join(TimeSpan.FromSeconds(15));
        _ready.Dispose();
    }
}
