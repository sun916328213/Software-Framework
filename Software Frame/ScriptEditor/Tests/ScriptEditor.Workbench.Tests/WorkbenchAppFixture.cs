// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchAppBootCollection、WorkbenchAppFixture。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、System.Threading、System.Windows、System.Windows.Threading、ScriptEditor.Workbench。导入不代表所有类型都会在运行时加载。
using Workbench.TestSupport;
using ScriptEditor.Controls;
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
    private WorkbenchTestLifetime? _lifetime;

    /// <summary>
    /// 初始化 WorkbenchAppFixture 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent、BeginInvoke、Set、Run、SetApartmentState；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public WorkbenchAppFixture()
    {
        _thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                _lifetime = new WorkbenchTestLifetime(app);
                app.InitializeComponent(); // 等价于生产入口：先载 App.xaml（壳层资源）
                app.Startup += (_, _) => app.Dispatcher.BeginInvoke(
                    new Action(() => { _lifetime.ObserveMainWindow(); _ready.Set(); }), DispatcherPriority.ApplicationIdle);
                app.Run();
            }
            catch (Exception ex)
            {
                _startupFault = ex;
                _lifetime?.RecordThreadFailure(ex);
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

    /// <summary>
    /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Exchange、InvokeShutdown、Join、FromSeconds、Dispose；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        (_lifetime ?? throw new InvalidOperationException("工作台未初始化退出处理。")).CloseAndWait(_thread);
        _ready.Dispose();
    }
}
