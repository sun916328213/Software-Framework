using System.IO;
using System.Windows;
using System.Windows.Threading;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using ScriptEditor.Services;
using Script.Tests.Core;
using Script.Tests.CoreTests;
using Script.Tests.UiTests;

namespace Script.Tests;

/// <summary>
/// Script.Tests 入口：
/// 依次执行核心功能验证/性能测试（无头 ScriptEngine 直驱）
/// 与 UI 稳定性/流畅性测试（真实 WPF 宿主窗口 + ScriptEditorView），
/// 记录关键性能指标并生成 Markdown + JSON 双格式测试报告。
/// </summary>
/// <remarks>
/// 泵消息模式：Main 不阻塞等待 async 任务（否则 Dispatcher 无法泵消息，
/// UI 测试中所有 Dispatcher.InvokeAsync 的 continuation 会死锁）。
/// 正确顺序：安装 DispatcherSynchronizationContext → 启动异步流程 →
/// Dispatcher.Run() 泵消息 → 流程结束时 app.Shutdown() 使 Run() 返回。
/// </remarks>
public static class Program
{
    private static int s_exitCode = 1;

    [STAThread]
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== Script.Tests 测试程序启动 ===");

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        // 关键：显式安装 DispatcherSynchronizationContext，
        // 确保 async continuation（await 后续）被投递回 UI 线程而非线程池
        System.Threading.SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(app.Dispatcher));

        // 在 UI 线程上启动异步测试流程（不阻塞 Main）
        _ = RunTestFlowAsync(app, args);

        // 泵消息直到 app.Shutdown()
        Dispatcher.Run();

        Console.WriteLine($"退出码: {s_exitCode}");
        return s_exitCode;
    }

    /// <summary>异步测试主流程：Setup → 注册模块 → 执行 → 报告 → Shutdown。</summary>
    private static async Task RunTestFlowAsync(Application app, string[] args)
    {
        try
        {
            // 支持参数：--core 只跑核心模块 / --ui 只跑 UI 模块（默认全跑）
            var onlyCore = args.Contains("--core", StringComparer.OrdinalIgnoreCase);
            var onlyUi = args.Contains("--ui", StringComparer.OrdinalIgnoreCase);
            if (onlyCore && onlyUi) { onlyCore = false; onlyUi = false; }

            InitializePrismContainer();
            MergeResourceDictionaries(app);

            var runner = new TestRunner();
            if (!onlyUi)
                CoreTestModule.Register(runner);
            if (!onlyCore)
                UiTestModule.Register(runner);

            var report = await runner.RunAllAsync(Console.Out);

            var reportDir = Path.Combine(AppContext.BaseDirectory, "TestReports");
            var mdPath = TestReporter.Write(report, reportDir);
            Console.WriteLine();
            Console.WriteLine(report.ToSummary());
            Console.WriteLine();
            Console.WriteLine($"报告已生成: {mdPath}");
            s_exitCode = report.FailedCount == 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"=== 测试程序异常终止: {ex} ===");
            s_exitCode = 1;
        }
        finally
        {
            // 通知 Main 退出 Dispatcher.Run
            app.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// 初始化 Prism 容器（P7：View/ViewModel 改由组合根显式装配，此处仅为旧宿主保留容器）。
    /// </summary>
    private static void InitializePrismContainer()
    {
        // Prism.DryIoc 提供 DryIocContainerExtension（与宿主 App 同一容器实现）
        var container = new Container();
        var ext = new DryIocContainerExtension(container);
        ContainerLocator.SetContainerExtension(() => ext);

        // ScriptEditorModule 中原本的注册（此处按测试宿主需要恢复）
        var registry = (IContainerRegistry)ext;
        registry.RegisterSingleton<ScriptEditorService>();
        registry.RegisterSingleton<FileService>();

        Console.WriteLine("[Setup] Prism 容器初始化完成");
    }

    /// <summary>
    /// 合并 ScriptEditor 视图所需资源字典（ScrollBar_Style 等来自 WPFCustomControls），
    /// 否则 ScriptEditorView.InitializeComponent 会因 StaticResource 缺失抛异常。
    /// </summary>
    private static void MergeResourceDictionaries(Application app)
    {
        try
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml",
                    UriKind.Absolute)
            };
            app.Resources.MergedDictionaries.Add(resources);
            Console.WriteLine("[Setup] 资源字典合并完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Setup] 资源字典合并失败（UI 测试可能受限）: {ex.Message}");
        }
    }
}
