// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WpfTestHost、Host。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Threading、MaterialDesignColors、MaterialDesignThemes.Wpf。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Threading;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;

namespace FlowEditor.Workbench.Tests;

/// <summary>让所有需要 Application 资源的 UI 测试复用同一个 STA Dispatcher。</summary>
internal static class WpfTestHost
{
    private static readonly Lazy<Host> SharedHost = new(() => new Host());

    /// <summary>
    /// 验证 Run 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke；具体条件和先后次序见方法体。
    /// </summary>
    public static void Run(Action action) => SharedHost.Value.Dispatcher.Invoke(action);

    /// <summary>
    /// Host 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    private sealed class Host
    {
        public Dispatcher Dispatcher { get; }

        /// <summary>
        /// 初始化 Host 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 EnsureResources、Set、Run、SetApartmentState、Start；具体条件和先后次序见方法体。
        /// </summary>
        public Host()
        {
            using var ready = new ManualResetEventSlim();
            Dispatcher? dispatcher = null;
            var thread = new Thread(() =>
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
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            Dispatcher = dispatcher!;
        }
    }

    /// <summary>
    /// 验证 EnsureResources 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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
