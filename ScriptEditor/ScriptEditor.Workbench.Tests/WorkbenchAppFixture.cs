using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ScriptEditor.Workbench;

namespace ScriptEditor.Workbench.Tests;

/// <summary>工作台 STA 宿主：整个测试集合只启动一次 App，断言经 Invoke 封送到 UI 线程。</summary>
[CollectionDefinition("WorkbenchAppBoot")]
public sealed class WorkbenchAppBootCollection : ICollectionFixture<WorkbenchAppFixture> { }

/// <summary>
/// STA 应用宿主夹具（WPF 单 Application 实例约束，各测各 new App 必挂）。
/// <para>
/// 走生产入口：<c>new App()</c> + <c>InitializeComponent()</c> 载入 App.xaml（壳层资源合并），
/// 再 <c>Run()</c> 由 <see cref="ScriptEditor.Workbench.App"/> 的 Prism 装配拉起主窗。
/// 与 Communication / Vision 的 Workbench.Tests 同一口径：宿主装配问题编译期不报错，
/// 只有把宿主真正拉起来才暴露（如 App.xaml 里的相对资源 URI 绑到入口程序集、
/// 模块注册与 CreateShell 的先后顺序）。
/// </para>
/// </summary>
public sealed class WorkbenchAppFixture : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private Exception? _startupFault;
    private int _disposed;

    public WorkbenchAppFixture()
    {
        _thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent(); // 等价于生产入口：先载 App.xaml（壳层资源）
                app.Startup += (_, _) => app.Dispatcher.BeginInvoke(
                    new Action(() => _ready.Set()), DispatcherPriority.ApplicationIdle);
                app.Run();
            }
            catch (Exception ex)
            {
                _startupFault = ex;
                _ready.Set();
            }
        })
        {
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(90)))
            throw new TimeoutException("ScriptEditor 工作台启动超时。");
        if (_startupFault is not null)
            throw new InvalidOperationException("ScriptEditor 工作台启动失败。", _startupFault);
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
            Application.Current?.Dispatcher.InvokeShutdown();
        }
        catch
        {
            // 关停期异常不掩盖测试结果。
        }
        _thread.Join(TimeSpan.FromSeconds(15));
        _ready.Dispose();
    }
}