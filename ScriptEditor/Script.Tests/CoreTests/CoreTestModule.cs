using System.Diagnostics;
using ScriptEditor.Services;
using Script.Tests.Core;

namespace Script.Tests.CoreTests;

/// <summary>
/// 核心执行程序的功能验证 + 性能测试模块（直接驱动无头 ScriptSession，无 UI 依赖）。
/// 场景：编译正确性、缓存命中、执行正确性、Inputs 注入、断点插桩、
/// 终止/取消、输出洪泛、长跑稳定性、并行一致性、内存增长。
/// </summary>
public static class CoreTestModule
{
    /// <summary>向调度器注册全部核心测试用例。</summary>
    public static void Register(TestRunner runner)
    {
        ScriptItemP2Tests.Register(runner);
        ScriptSessionP3Tests.Register(runner);
        const string cat = "核心功能验证";
        const string perfCat = "性能测试";

        // ── 功能验证：编译 ──────────────────────────────
        runner.Register(cat, "编译-正确脚本成功", async ctx =>
        {
            var engine = new ScriptSession(() => "Console.WriteLine(\"ok\");");
            await engine.CompileAsync();
            ctx.Assert(engine.StatusText == "编译完成", $"期望状态=编译完成，实际={engine.StatusText}");
            ctx.Assert(engine.Output.Contains("✓", StringComparison.Ordinal), $"编译成功标记缺失: {engine.Output}");
        });

        runner.Register(cat, "编译-语法错误被诊断", async ctx =>
        {
            var engine = new ScriptSession(() => "var x = ;");
            await engine.CompileAsync();
            ctx.Assert(engine.StatusText == "编译失败", $"期望状态=编译失败，实际={engine.StatusText}");
            ctx.Assert(engine.Output.Contains("行", StringComparison.Ordinal), $"诊断缺少行号信息: {engine.Output}");
        });

        runner.Register(cat, "编译-失败不污染缓存", async ctx =>
        {
            var okCode = "Console.WriteLine(\"v1\");";
            var engine = new ScriptSession(() => okCode);
            var ok = await engine.RunAsync();
            ctx.Assert(ok, "首次执行应成功");

            // 改为错误代码编译失败后，再改回正确代码：应重新编译执行而不是误命中缓存
            var code = okCode;
            var mutable = new Func<string>(() => code);
            var engine2 = new ScriptSession(mutable);
            await engine2.CompileAsync();
            code = "var y = ;";
            await engine2.CompileAsync();
            ctx.Assert(engine2.StatusText == "编译失败", "错误代码应编译失败");
            code = okCode;
            var ok2 = await engine2.RunAsync();
            ctx.Assert(ok2, $"失败后改回正确代码应执行成功，实际状态={engine2.StatusText}");
        });

        // ── 功能验证：执行 ──────────────────────────────
        runner.Register(cat, "执行-Console输出捕获", async ctx =>
        {
            var engine = new ScriptSession(() => "Console.WriteLine(\"hello-core\");");
            var ok = await engine.RunAsync();
            ctx.Assert(ok, $"执行失败: {engine.StatusText}");
            ctx.Assert(engine.Output.Contains("hello-core", StringComparison.Ordinal), $"输出未捕获: {engine.Output}");
            ctx.Assert(engine.StatusText == "执行成功", $"状态={engine.StatusText}");
        });

        runner.Register(cat, "执行-返回值与LastScriptResult", async ctx =>
        {
            var engine = new ScriptSession(() => "40 + 2");
            var ok = await engine.RunAsync();
            ctx.Assert(ok, "执行失败");
            ctx.Assert(engine.Output.Contains("42", StringComparison.Ordinal), $"返回值未输出: {engine.Output}");
            ctx.Assert(engine.LastResult is 42, $"LastScriptResult={engine.LastResult}");
        });

        runner.Register(cat, "执行-Inputs字典注入", async ctx =>
        {
            var engine = new ScriptSession(() => "Console.WriteLine($\"a={Inputs[\"k\"]}\");");
            engine.SetInputData(new Dictionary<string, object?> { ["k"] = 77 });
            var ok = await engine.RunAsync();
            ctx.Assert(ok, $"执行失败: {engine.StatusText}/{engine.Output}");
            ctx.Assert(engine.Output.Contains("a=77", StringComparison.Ordinal), $"注入值未生效: {engine.Output}");
        });

        runner.Register(cat, "执行-Script类模板自动Run", async ctx =>
        {
            var engine = new ScriptSession(() => """
                public class Script
                {
                    public void Run() { Console.WriteLine("script-run-ok"); }
                }
                """);
            var ok = await engine.RunAsync();
            ctx.Assert(ok, $"执行失败: {engine.Output}");
            ctx.Assert(engine.Output.Contains("script-run-ok", StringComparison.Ordinal), $"Run 未被自动调用: {engine.Output}");
        });

        runner.Register(cat, "执行-Globals完整注入路径", async _ =>
        {
            // 覆盖 Dictionary 索引、class 成员、List 集合、嵌套成员、静态/实例 Run 等注入形式
            await ScriptEngineGlobalsTest.RunAsync();
        });

        runner.Register(cat, "执行-运行时异常标记失败", async ctx =>
        {
            var engine = new ScriptSession(() => "throw new InvalidOperationException(\"boom\");");
            var ok = await engine.RunAsync();
            ctx.Assert(!ok, "抛异常的脚本不应视为成功");
            ctx.Assert(engine.StatusText == "执行失败", $"状态={engine.StatusText}");
            ctx.Assert(engine.Output.Contains("boom", StringComparison.Ordinal), $"异常消息未呈现: {engine.Output}");
        });

        runner.Register(cat, "执行-空代码友好提示", async ctx =>
        {
            var engine = new ScriptSession(() => "");
            var ok = await engine.RunAsync();
            ctx.Assert(!ok, "空代码应返回 false");
            ctx.Assert(engine.Output.Contains("请输入", StringComparison.Ordinal), $"提示缺失: {engine.Output}");
        });

        // ── 功能验证：断点插桩 ──────────────────────────
        runner.Register(cat, "断点-插桩代码仍可执行", async ctx =>
        {
            var code = "Console.WriteLine(\"bp-line\");";
            var engine = new ScriptSession(() => code, getBreakpoints: () => new[] { 1 });
            var ok = await engine.RunAsync();
            ctx.Assert(ok, $"带断点执行失败: {engine.StatusText}/{engine.Output}");
            ctx.Assert(engine.Output.Contains("bp-line", StringComparison.Ordinal), $"插桩后输出异常: {engine.Output}");
        });

        // ── 功能验证：终止 ──────────────────────────────
        runner.Register(cat, "终止-置状态且引擎可复用", async ctx =>
        {
            // 真实限制：Roslyn 脚本在 Task.Run 的线程池线程上同步执行，
            // 非协作死循环（不检查 CancellationToken）无法被强制中断
            // （.NET Core 无 Thread.Abort）。本用例验证 Terminate 的实际契约：
            // 1) 立即更新 IsRunning/StatusText；2) 引擎可继续执行后续脚本。
            string code = "while (true) { System.Threading.Thread.Sleep(100); }";
            var engine = new ScriptSession(() => code);
            _ = engine.RunAsync(); // 后台无限循环（刻意不 await）
            await Task.Delay(800);  // 等编译完成进入循环
            engine.Terminate();
            ctx.Assert(!engine.IsRunning, $"Terminate 后 IsRunning 应为 false，实际={engine.IsRunning}");
            ctx.Assert(engine.StatusText == "正在终止...", $"状态={engine.StatusText}");

            // 引擎复用：更换代码后应正常执行新脚本
            code = "Console.WriteLine(\"reuse-ok\");";
            var ok = await engine.RunAsync();
            ctx.Assert(ok, $"Terminate 后引擎未恢复: {engine.StatusText}");
            ctx.Assert(engine.Output.Contains("reuse-ok", StringComparison.Ordinal),
                $"复用执行输出异常: {engine.Output}");
        });

        // ── 功能验证：循环执行 ──────────────────────────
        runner.Register(cat, "循环-迭代分隔与停止", async ctx =>
        {
            var engine = new ScriptSession(() => "Console.WriteLine(\"tick\");")
            {
                LoopInterval = 50
            };
            var loopTask = engine.LoopRunAsync();
            await Task.Delay(700);
            engine.StopLoop();
            var completed = await Task.WhenAny(loopTask, Task.Delay(5000)) == loopTask;
            ctx.Assert(completed, "StopLoop 后 5 秒内未结束");
            ctx.Assert(!engine.IsLooping && !engine.IsRunning, $"停止后状态异常: IsLooping={engine.IsLooping}, IsRunning={engine.IsRunning}");
            ctx.Assert(engine.Output.Contains("tick", StringComparison.Ordinal), "循环输出缺失");
        });

        // ── 性能：编译缓存命中 ──────────────────────────
        runner.Register(perfCat, "编译-缓存命中加速", async ctx =>
        {
            var code = "Console.WriteLine(\"cache\");";
            var engine = new ScriptSession(() => code);
            // 第一次：编译 + 执行
            var sw1 = Stopwatch.StartNew();
            var ok1 = await engine.RunAsync();
            sw1.Stop();
            // 第二次：相同代码应命中编译缓存（耗时显著下降）
            var sw2 = Stopwatch.StartNew();
            var ok2 = await engine.RunAsync();
            sw2.Stop();
            ctx.Assert(ok1 && ok2, "两次执行均应成功");
            ctx.Metric("首次耗时", sw1.Elapsed.TotalMilliseconds);
            ctx.Metric("缓存命中耗时", sw2.Elapsed.TotalMilliseconds);
            ctx.Metric("加速比", sw2.Elapsed.TotalMilliseconds < sw1.Elapsed.TotalMilliseconds * 0.5
                ? ">2x（缓存生效）" : "无显著加速（需关注）");
        });

        runner.Register(perfCat, "预编译-加速首次执行", async ctx =>
        {
            var code = "Console.WriteLine(\"pre\");";
            var engine = new ScriptSession(() => code);
            await engine.PreCompileAsync(code); // 后台预编译填充缓存
            var sw = Stopwatch.StartNew();
            var ok = await engine.RunAsync();
            sw.Stop();
            ctx.Assert(ok, "执行失败");
            ctx.Metric("预编译后执行耗时", sw.Elapsed.TotalMilliseconds);
            ctx.Assert(sw.Elapsed.TotalMilliseconds < 2000, $"预编译后首次执行过慢: {sw.Elapsed.TotalMilliseconds:F0} ms");
        });

        // ── 性能：输出洪泛吞吐 ──────────────────────────
        runner.Register(perfCat, "输出-洪泛1000行吞吐", async ctx =>
        {
            var code = "for (int i = 0; i < 1000; i++) Console.WriteLine($\"line-{i}\");";
            var engine = new ScriptSession(() => code);
            var sw = Stopwatch.StartNew();
            var ok = await engine.RunAsync();
            sw.Stop();
            ctx.Assert(ok, "执行失败");
            engine.FlushOutput();
            var lines = engine.Output.Split('\n').Length;
            ctx.Metric("总耗时", sw.Elapsed.TotalMilliseconds);
            ctx.Metric("输出行数", $"{lines}");
            ctx.Assert(sw.Elapsed.TotalMilliseconds < 15000, $"1000 行输出耗时过长: {sw.Elapsed.TotalMilliseconds:F0} ms");
        });

        // ── 性能：长跑稳定性 ────────────────────────────
        runner.Register(perfCat, "长跑-300次连续执行", async ctx =>
        {
            const int N = 300;
            var engine = new ScriptSession(() => "var s = 0; for (int i = 0; i < 1000; i++) s += i; Console.WriteLine(s);");
            var sw = Stopwatch.StartNew();
            int failures = 0;
            for (int i = 0; i < N; i++)
            {
                if (!await engine.RunAsync()) failures++;
            }
            sw.Stop();
            ctx.Metric("平均单次耗时", sw.Elapsed.TotalMilliseconds / N);
            ctx.Metric("失败次数", $"{failures}");
            ctx.Assert(failures == 0, $"{failures}/{N} 次执行失败");
        });

        // ── 性能：并行执行一致性 ────────────────────────
        runner.Register(perfCat, "并行-8引擎同时执行", async ctx =>
        {
            const int count = 8;
            var engines = new (ScriptSession Engine, int Id)[count];
            for (int i = 0; i < count; i++)
            {
                int id = i;
                engines[i] = (new ScriptSession(() => $"Console.WriteLine(\"engine-{id}\");"), id);
            }
            var sw = Stopwatch.StartNew();
            var tasks = engines.Select(async pair =>
            {
                var ok = await pair.Engine.RunAsync();
                return (pair.Id, ok, pair.Engine.Output);
            }).ToArray();
            var results = await Task.WhenAll(tasks);
            sw.Stop();
            ctx.Metric("并行总耗时", sw.Elapsed.TotalMilliseconds);
            foreach (var (id, ok, output) in results)
            {
                ctx.Assert(ok, $"engine-{id} 执行失败: {output}");
                ctx.Assert(output.Contains($"engine-{id}", StringComparison.Ordinal),
                    $"engine-{id} 输出串扰（输出路由到了别的引擎）: {output}");
            }
        });

        // ── 性能：内存增长 ──────────────────────────────
        runner.Register(perfCat, "内存-多次重编译内存稳定", async ctx =>
        {
            // 每轮代码不同，强制走重新编译路径（CleanupOldScript 应清理旧脚本类型）
            const int rounds = 40;
            long peakWorkingSet = 0;
            for (int i = 0; i < rounds; i++)
            {
                int idx = i;
                var engine = new ScriptSession(() => $"Console.WriteLine(\"mem-round-{idx}\");");
                var ok = await engine.RunAsync();
                ctx.Assert(ok, $"第 {idx} 轮执行失败");
                peakWorkingSet = Math.Max(peakWorkingSet, Environment.WorkingSet);
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var after = Environment.WorkingSet;
            ctx.Metric("峰值工作集", $"{peakWorkingSet / 1024.0 / 1024:F1} MB");
            ctx.Metric("结束后工作集", $"{after / 1024.0 / 1024:F1} MB");
            ctx.Metric("增长比", $"{(double)after / peakWorkingSet:P1}");
            ctx.Assert(after < peakWorkingSet * 1.5,
                $"GC 后工作集异常增长: 峰值 {peakWorkingSet / 1048576:F1} MB → 结束 {after / 1048576:F1} MB");
        });
    }
}
