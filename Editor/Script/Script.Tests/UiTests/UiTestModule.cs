using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ScriptEditor.Models;
using ScriptEditor.Views;
using Script.Tests.Core;

namespace Script.Tests.UiTests;

/// <summary>
/// ScriptEditorView UI 稳定性 + 流畅性测试模块。
/// 场景：编辑器加载耗时、帧率采样、高频输出滚动流畅性、多标签切页稳定性、
/// 视图反复 Attach/Detach（生命周期验证）。
/// 前置：Application 已初始化（[STAThread] + PushFrame 泵消息），资源字典已合并。
/// </summary>
public static class UiTestModule
{
    /// <summary>向调度器注册全部 UI 测试用例。</summary>
    public static void Register(TestRunner runner)
    {
        const string stabilityCat = "UI 稳定性";
        const string smoothCat = "UI 流畅性";

        // ── 稳定性：编辑器加载 ──────────────────────────
        runner.Register(stabilityCat, "加载-引用程序集解析", ScriptEditorLoadTest.RunAsync);

        runner.Register(stabilityCat, "加载-ScriptEditorView初始化", async ctx =>
        {
            var (view, window) = CreateEditorHost();
            try
            {
                view.DataContext = MakeItem("加载测试", "Console.WriteLine(\"ui-init\");");
                var sw = Stopwatch.StartNew();
                var ok = await view.EnsureInitializedAsync(timeoutMs: 60000);
                sw.Stop();
                ctx.Assert(ok, "编辑器初始化超时");
                ctx.Metric("初始化耗时", sw.Elapsed.TotalMilliseconds);
                ctx.Assert(sw.Elapsed.TotalMilliseconds < 30000, $"初始化过慢: {sw.Elapsed.TotalMilliseconds:F0} ms");
            }
            finally
            {
                CloseHost(window);
            }
        });

        // ── 稳定性：多标签快速切页 ────────────────────────
        runner.Register(stabilityCat, "切页-8标签50次切换", async ctx =>
        {
            const int tabCount = 8;
            const int switches = 50;
            var (view, window) = CreateEditorHost();
            try
            {
                view.DataContext = MakeItem("tab-0", "Console.WriteLine(\"tab-0\");");
                ctx.Assert(await view.EnsureInitializedAsync(60000), "初始化超时");

                var sw = Stopwatch.StartNew();
                string lastName = "tab-0";
                for (int i = 0; i < switches; i++)
                {
                    lastName = $"tab-{i % tabCount}";
                    // 直接切换 DataContext 模拟 TabControl 切页路径（OnDataContextChanged 同步换内容）
                    view.DataContext = MakeItem(lastName, $"Console.WriteLine(\"{lastName}\");");
                    // 每次切页让 Dispatcher 处理挂起的布局任务
                    await view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
                sw.Stop();
                ctx.Metric("总切换耗时", sw.Elapsed.TotalMilliseconds);
                ctx.Metric("单次平均", sw.Elapsed.TotalMilliseconds / switches);
                // 内容切换正确性：编辑器应已装载最后一个脚本的代码
                var code = view.GetCurrentCode();
                ctx.Assert(code.Contains(lastName, StringComparison.Ordinal),
                    $"切页后内容错误（期望含 {lastName}）: {code}");
                ctx.Assert(sw.Elapsed.TotalMilliseconds / switches < 500,
                    $"单次切页过慢: {sw.Elapsed.TotalMilliseconds / switches:F0} ms");
            }
            finally
            {
                CloseHost(window);
            }
        });

        // ── 稳定性：视图反复加载卸载（生命周期验证）──────
        runner.Register(stabilityCat, "生命周期-反复Loaded/Unloaded", async ctx =>
        {
            const int rounds = 5;
            var (view, window) = CreateEditorHost();
            try
            {
                view.DataContext = MakeItem("lifecycle", "Console.WriteLine(\"lc\");");
                ctx.Assert(await view.EnsureInitializedAsync(60000), "初始化超时");

                for (int i = 0; i < rounds; i++)
                {
                    // 从窗口移除再放回，触发 Unloaded → 延迟 Detach → Loaded → Attach
                    window.Content = null;
                    await view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    window.Content = view;
                    await view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
                // 反复加载卸载后视图应仍可正常执行
                await view.ExecuteAsync();
                ctx.Assert(view.GetCurrentCode().Contains("lc", StringComparison.Ordinal),
                    "反复加载卸载后内容丢失");
                ctx.Metric("验证轮次", $"{rounds}");
            }
            finally
            {
                CloseHost(window);
            }
        });

        // ── 流畅性：帧率采样 ─────────────────────────────
        runner.Register(smoothCat, "帧率-空闲渲染采样", async ctx =>
        {
            var (view, window) = CreateEditorHost();
            try
            {
                view.DataContext = MakeItem("fps", "Console.WriteLine(\"fps\");");
                await view.EnsureInitializedAsync(60000);

                // CompositionTarget.Rendering 采样 3 秒帧间隔
                const int sampleMs = 3000;
                var frameStamps = new List<long>();
                var start = Stopwatch.StartNew();
                var frameWatch = Stopwatch.StartNew();
                EventHandler renderHandler = (_, _) =>
                {
                    frameStamps.Add(frameWatch.ElapsedMilliseconds);
                };
                CompositionTarget.Rendering += renderHandler;
                try
                {
                    while (start.ElapsedMilliseconds < sampleMs)
                        await view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
                finally
                {
                    CompositionTarget.Rendering -= renderHandler;
                }

                var intervals = new List<double>();
                for (int i = 1; i < frameStamps.Count; i++)
                    intervals.Add(frameStamps[i] - frameStamps[i - 1]);
                if (intervals.Count == 0)
                {
                    ctx.Fail("渲染事件未触发（无 UI 帧采样）");
                    return;
                }
                var avgFps = 1000.0 / intervals.Average();
                var maxInterval = intervals.Max();
                ctx.Metric("平均帧率", $"{avgFps:F1} FPS");
                ctx.Metric("最大帧间隔", $"{maxInterval} ms");
                ctx.Metric("采样帧数", $"{frameStamps.Count}");
                // 空闲场景：平均 ≥ 20 FPS 且无 >1000ms 冻结帧
                ctx.Assert(avgFps >= 20, $"空闲平均帧率过低: {avgFps:F1} FPS");
                ctx.Assert(maxInterval < 1000, $"出现长冻结帧: {maxInterval} ms");
            }
            finally
            {
                CloseHost(window);
            }
        });

        // ── 流畅性：高频输出滚动 ─────────────────────────
        runner.Register(smoothCat, "输出-洪泛时UI响应", async ctx =>
        {
            var (view, window) = CreateEditorHost();
            try
            {
                view.DataContext = MakeItem("flood", "Console.WriteLine(\"flood-seed\");");
                await view.EnsureInitializedAsync(60000);

                // 通过公共 API 执行一个洪泛脚本，同时采样 UI 线程调度延迟
                var code = "for (int i = 0; i < 2000; i++) Console.WriteLine($\"flood-line-{i}\");";
                view.SetScriptCode(code);

                var maxDispatchLatency = 0L;
                var sampling = true;
                _ = Task.Run(async () =>
                {
                    while (sampling)
                    {
                        // 从后台线程探测 UI 线程的调度延迟（洪泛期间 UI 不应长时间冻结）
                        var t = view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                        var sw = Stopwatch.StartNew();
                        await t;
                        maxDispatchLatency = Math.Max(maxDispatchLatency, sw.ElapsedMilliseconds);
                    }
                });

                var swRun = Stopwatch.StartNew();
                await view.ExecuteAsync();
                swRun.Stop();
                sampling = false;
                await Task.Delay(400); // 等输出冲刷/绑定传播完成

                ctx.Assert(view.OutputText.Contains("flood-line-", StringComparison.Ordinal),
                    $"洪泛输出未到达 UI: {view.OutputText[..Math.Min(200, view.OutputText.Length)]}");
                ctx.Metric("执行总耗时", swRun.Elapsed.TotalMilliseconds);
                ctx.Metric("UI 最大调度延迟", $"{maxDispatchLatency} ms");
                // 阈值较宽（脚本后台执行 + 输出节流绑定）：不应出现 >2000ms 的 UI 冻结
                ctx.Assert(maxDispatchLatency < 2000,
                    $"洪泛期间 UI 调度延迟过大: {maxDispatchLatency} ms");
            }
            finally
            {
                CloseHost(window);
            }
        });

        // ── 流畅性：切页帧间隔 ───────────────────────────
        runner.Register(smoothCat, "切页-切换期间帧间隔", async ctx =>
        {
            var (view, window) = CreateEditorHost();
            try
            {
                view.DataContext = MakeItem("sw-fps-0", "Console.WriteLine(\"sw\");");
                await view.EnsureInitializedAsync(60000);

                var frameWatch = Stopwatch.StartNew();
                var stamps = new List<long>();
                EventHandler handler = (_, _) => stamps.Add(frameWatch.ElapsedMilliseconds);
                CompositionTarget.Rendering += handler;
                try
                {
                    for (int i = 0; i < 20; i++)
                    {
                        view.DataContext = MakeItem($"sw-fps-{i}", $"Console.WriteLine(\"sw-{i}\");");
                        await view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    }
                }
                finally
                {
                    CompositionTarget.Rendering -= handler;
                }

                var intervals = new List<double>();
                for (int i = 1; i < stamps.Count; i++)
                    intervals.Add(stamps[i] - stamps[i - 1]);
                if (intervals.Count > 0)
                {
                    ctx.Metric("平均帧间隔", $"{intervals.Average():F1} ms");
                    ctx.Metric("最大帧间隔", $"{intervals.Max():F0} ms");
                    ctx.Assert(intervals.Max() < 3000, $"切页出现长冻结帧: {intervals.Max():F0} ms");
                }
                else
                {
                    ctx.Fail("切页期间无渲染帧采样");
                }
            }
            finally
            {
                CloseHost(window);
            }
        });
    }

    // ════════════════════════════════════════════════
    //  辅助：宿主窗口与脚本项构造
    // ════════════════════════════════════════════════

    /// <summary>创建承载 ScriptEditorView 的测试宿主窗口并显示。</summary>
    private static (ScriptEditorView View, Window Window) CreateEditorHost()
    {
        var view = new ScriptEditorView();
        var window = new Window
        {
            Title = "Script.Tests UI 测试宿主",
            Width = 1200,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = view
        };
        window.Show();
        // 泵一轮 Dispatcher，确保 Loaded 已触发
        DispatcherUtil.Pump();
        return (view, window);
    }

    /// <summary>关闭宿主窗口并等待 Detach 延迟任务完成。</summary>
    private static void CloseHost(Window window)
    {
        window.Close();
        DispatcherUtil.Pump();
    }

    /// <summary>构造模拟 ScriptItem。</summary>
    private static ScriptItem MakeItem(string title, string code) => new()
    {
        Title = title,
        Code = code
    };
}

/// <summary>Dispatcher 辅助：泵空转帧直到空闲。</summary>
internal static class DispatcherUtil
{
    /// <summary>依次排空 Background 与 ApplicationIdle 优先级的队列各一轮。</summary>
    public static void Pump()
    {
        var d = Application.Current?.Dispatcher;
        if (d == null) return;
        d.Invoke(() => { }, DispatcherPriority.Background);
        d.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
