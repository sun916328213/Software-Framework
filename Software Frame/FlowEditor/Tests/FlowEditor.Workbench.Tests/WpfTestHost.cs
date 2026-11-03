// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WpfTestHost、Host。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Threading、MaterialDesignColors、MaterialDesignThemes.Wpf。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Threading;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>让所有需要 Application 资源的 UI 测试复用同一个 STA Dispatcher。</summary>
internal static class WpfTestHost
{
    private static readonly Lazy<Host> SharedHost = new(() => new Host());

    /// <summary>
    /// 测试相关入口 Run；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke；具体条件和先后次序见方法体。
    /// </summary>
    public static void Run(Action action) => SharedHost.Value.Dispatcher.Invoke(action);

    internal static void Shutdown()
    {
        if (SharedHost.IsValueCreated) SharedHost.Value.Dispose();
    }

    /// <summary>
    /// Host 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    private sealed class Host : IDisposable
    {
        public Dispatcher Dispatcher { get; }
        private readonly Thread _thread;

        /// <summary>
        /// 初始化 Host 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 EnsureResources、Set、Run、SetApartmentState、Start；具体条件和先后次序见方法体。
        /// </summary>
        public Host()
        {
            using var ready = new ManualResetEventSlim();
            Dispatcher? dispatcher = null;
            _thread = new Thread(() =>
            {
                var application = Application.Current ?? new Application();
                EnsureResources(application);
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "FlowEditor WPF test host"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            ready.Wait();
            Dispatcher = dispatcher!;
        }

        public void Dispose()
        {
            // 在xUnit集合收尾（不是ProcessExit）于原STA清理弱事件表。
            // BeginInvoke使发起退出的调度操作先完成，避免同步Shutdown取消其自身Task。
            if (!Dispatcher.HasShutdownStarted)
            {
                var cleanup = Dispatcher.InvokeAsync(() => Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background), DispatcherPriority.Send);
                if (!cleanup.Task.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("FlowEditor测试STA未处理退出请求。");
                cleanup.Task.GetAwaiter().GetResult();
            }
            if (!_thread.Join(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("FlowEditor测试STA未正常退出。");
        }
    }

    /// <summary>
    /// 测试相关入口 EnsureResources；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
    /// </summary>
    private static void EnsureResources(Application application)
    {
        application.Resources.MergedDictionaries.Add(new BundledTheme
        {
            BaseTheme = BaseTheme.Light,
            PrimaryColor = PrimaryColor.Teal,
            SecondaryColor = SecondaryColor.Amber
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/FlowEditor.Controls;component/Resources/Resources.xaml")
        });
    }
}
