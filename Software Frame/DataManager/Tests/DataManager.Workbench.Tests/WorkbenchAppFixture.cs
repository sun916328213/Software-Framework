// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchAppBootCollection、WorkbenchAppFixture。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Workbench、System.Windows、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using Workbench.TestSupport;
using DataManager.Core;
using DataManager.Workbench;
using System.Windows;
using System.Windows.Threading;

namespace DataManager.Workbench.Tests
{
    /// <summary>
    /// WorkbenchAppBootCollection 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICollectionFixture&lt;WorkbenchAppFixture&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    [CollectionDefinition("WorkbenchAppBoot")]
    public sealed class WorkbenchAppBootCollection : ICollectionFixture<WorkbenchAppFixture> { }

    /// <summary>
    /// 工作台 STA 宿主：整个测试集合只启动一次 Prism App（WPF 同一进程只允许一个
    /// Application 实例，Shutdown 后也不释放，各测各 new App 必挂）。
    /// 测试经 <see cref="Invoke"/> 把断言封送到 UI 线程；集合跑完才关停。
    /// </summary>
    public sealed class WorkbenchAppFixture : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _ready = new(false);
        private int _disposed;
        private WorkbenchTestLifetime? _lifetime;

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
                app.InitializeComponent(); // 等价于生产入口：先载 App.xaml 资源再 Run
                app.Startup += (_, _) => app.Dispatcher.BeginInvoke(
                    new Action(() => { _lifetime.ObserveMainWindow(); _ready.Set(); }), DispatcherPriority.ApplicationIdle);
                app.Run();
            });
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            if (!_ready.Wait(TimeSpan.FromSeconds(90)))
                throw new TimeoutException("工作台启动超时");
        }

        /// <summary>在 UI 线程同步执行（异常直接抛回调用方）。</summary>
        public void Invoke(Action action) => Application.Current.Dispatcher.Invoke(action);

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
}
