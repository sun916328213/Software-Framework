using System.Reflection;
using System.Windows.Threading;
using Script.Tests.Core;
using ScriptEditor.Contracts;
using ScriptEditor.Models;
using ScriptEditor.Services;

namespace Script.Tests.CoreTests;

/// <summary>
/// P3「引入 ScriptSession，统一运行状态」的验收用例。
/// 覆盖阶段完成条件：单一事实源 / 切页不改变运行事实 / 多个脚本并行状态彼此隔离。
/// </summary>
public static class ScriptSessionP3Tests
{
    private const string Cat = "P3 会话状态";

    public static void Register(TestRunner runner)
    {
        RegisterSingleSourceOfTruth(runner);
        RegisterTabSwitchKeepsRunFacts(runner);
        RegisterParallelIsolation(runner);
        RegisterLifecycle(runner);
    }

    // ── 单一事实源 ────────────────────────────────────────
    private static void RegisterSingleSourceOfTruth(TestRunner runner)
    {
        runner.Register(Cat, "单一事实源-ScriptItem 不再持有运行状态", ctx =>
        {
            // 数据对象只描述「脚本是什么」：运行状态属性必须已从 ScriptItem 上移除。
            var runStateNames = new[]
            {
                "OutputText", "StatusText", "IsRunning", "IsLooping",
                "LoopInterval", "ExecutionTime", "ItemStatus"
            };
            var properties = typeof(ScriptItem).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var name in runStateNames)
                ctx.Assert(!properties.Contains(name), $"ScriptItem 不应再暴露运行状态属性 {name}");

            ctx.Assert(typeof(ScriptItem).BaseType?.Name != "ScriptStateBase",
                "ScriptItem 不应再继承 ScriptStateBase（运行状态已迁出）");
        });

        runner.Register(Cat, "单一事实源-运行状态只存在于会话", async ctx =>
        {
            var item = new ScriptItem { Code = "Console.WriteLine(\"p3-single-source\");" };
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                var session = runtime.GetSession(item);
                ctx.Assert(session is IScriptSession, "运行时应返回会话契约实例");
                ctx.Assert(session.CompilationState == ScriptCompilationState.NotCompiled,
                    $"未编译前应为 NotCompiled，实际={session.CompilationState}");

                var ok = await session.RunAsync();
                await Dispatcher.Yield(DispatcherPriority.Background);
                ctx.Assert(ok, $"执行失败: {session.StatusText}");
                ctx.Assert(session.Output.Contains("p3-single-source", StringComparison.Ordinal),
                    $"输出未落在会话上: {session.Output}");
                ctx.Assert(session.StatusText == "执行成功", $"状态应落在会话上，实际={session.StatusText}");
                ctx.Assert(session.CompilationState == ScriptCompilationState.Compiled,
                    $"成功执行后应为 Compiled，实际={session.CompilationState}");
                ctx.Assert(session.IsRunning == false, "执行结束后 IsRunning 应为 false");
            }
            finally { runtime.ReleaseSession(item); }
        });

        runner.Register(Cat, "单一事实源-同一脚本始终复用同一会话", async ctx =>
        {
            var item = new ScriptItem { Code = "Console.WriteLine(\"reuse-session\");" };
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                ctx.Assert(runtime.FindSession(item) == null, "未获取前不应存在会话");

                var first = runtime.GetSession(item);
                var second = runtime.GetSession(item);
                ctx.Assert(ReferenceEquals(first, second), "同一脚本对象必须复用同一会话实例");
                ctx.Assert(ReferenceEquals(runtime.FindSession(item), first), "FindSession 应返回同一会话");

                await first.RunAsync();
                ctx.Assert(runtime.GetSession(item).Output.Contains("reuse-session", StringComparison.Ordinal),
                    "重新获取会话应保留既有运行事实");
            }
            finally { runtime.ReleaseSession(item); }
        });

        runner.Register(Cat, "单一事实源-编译状态机随会话迁移", async ctx =>
        {
            var item = new ScriptItem { Code = "var x = ;" };
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                var session = runtime.GetSession(item);
                ctx.Assert(session.CompilationState == ScriptCompilationState.NotCompiled, "初始应为 NotCompiled");

                var failed = await session.CompileAsync();
                ctx.Assert(!failed, "语法错误应编译失败");
                ctx.Assert(session.CompilationState == ScriptCompilationState.Failed,
                    $"编译失败后应为 Failed，实际={session.CompilationState}");

                item.Code = "Console.WriteLine(\"fixed\");";
                var fixedOk = await session.CompileAsync();
                ctx.Assert(fixedOk, "修正后应编译成功");
                ctx.Assert(session.CompilationState == ScriptCompilationState.Compiled,
                    $"编译成功后应为 Compiled，实际={session.CompilationState}");
            }
            finally { runtime.ReleaseSession(item); }
        });

        runner.Register(Cat, "单一事实源-会话标题跟随脚本数据", ctx =>
        {
            var item = new ScriptItem { Title = "原名" };
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                var session = runtime.GetSession(item);
                ctx.Assert(session.Title == "原名", $"会话标题应读取脚本标题，实际={session.Title}");
                item.Title = "改名";
                ctx.Assert(session.Title == "改名", $"标题变更应即时反映，实际={session.Title}");
                ctx.Assert(session.ScriptId == item.ScriptId, "会话应携带脚本永久标识");
            }
            finally { runtime.ReleaseSession(item); }
        });
    }

    // ── 切页不改变运行事实 ────────────────────────────────
    private static void RegisterTabSwitchKeepsRunFacts(TestRunner runner)
    {
        runner.Register(Cat, "切页-切换后运行事实不丢失", async ctx =>
        {
            var first = new ScriptItem { Code = "Console.WriteLine(\"tab-a-output\");" };
            var second = new ScriptItem { Code = "Console.WriteLine(\"tab-b-output\");" };
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                var sessionA = runtime.GetSession(first);
                await sessionA.RunAsync();
                var capturedOutput = sessionA.Output;

                // 模拟切到另一页：解析另一脚本的会话，原会话不受任何影响
                var sessionB = runtime.GetSession(second);
                await sessionB.RunAsync();

                // 再切回第一页：拿到的仍是同一会话、同一运行事实
                var backToA = runtime.GetSession(first);
                ctx.Assert(ReferenceEquals(backToA, sessionA), "切回后应拿到原会话");
                ctx.Assert(backToA.Output == capturedOutput, "切页不应改变既有输出");
                ctx.Assert(backToA.Output.Contains("tab-a-output", StringComparison.Ordinal),
                    "第一页输出应保留");
                ctx.Assert(!backToA.Output.Contains("tab-b-output", StringComparison.Ordinal),
                    "第一页输出不应混入第二页内容");
            }
            finally
            {
                runtime.ReleaseSession(first);
                runtime.ReleaseSession(second);
            }
        });

        runner.Register(Cat, "切页-运行中切页不打断执行", async ctx =>
        {
            var running = new ScriptItem { Code = "Console.WriteLine(\"tick\");" };
            var other = new ScriptItem { Code = "Console.WriteLine(\"other\");" };
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                var session = runtime.GetSession(running);
                session.LoopInterval = 50;
                var loopTask = session.LoopRunAsync();
                await Task.Delay(500);
                ctx.Assert(session.IsLooping, "循环启动后 IsLooping 应为 true");

                // 切页：解析并执行另一脚本的会话
                var otherSession = runtime.GetSession(other);
                await otherSession.RunAsync();

                // 切回：原会话仍在循环，运行事实未被切页改变
                var backToRunning = runtime.GetSession(running);
                ctx.Assert(ReferenceEquals(backToRunning, session), "切回后应拿到原会话");
                ctx.Assert(backToRunning.IsLooping, "切页不应停止正在循环的脚本");
                ctx.Assert(backToRunning.IsRunning, "切页不应打断正在运行的脚本");

                session.StopLoop();
                var completed = await Task.WhenAny(loopTask, Task.Delay(5000)) == loopTask;
                ctx.Assert(completed, "StopLoop 后循环应在 5 秒内结束");
            }
            finally
            {
                runtime.ReleaseSession(running);
                runtime.ReleaseSession(other);
            }
        });
    }

    // ── 多个脚本并行状态彼此隔离 ──────────────────────────
    private static void RegisterParallelIsolation(TestRunner runner)
    {
        runner.Register(Cat, "并行-输出彼此隔离不串扰", async ctx =>
        {
            const int count = 6;
            var items = new ScriptItem[count];
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                for (int i = 0; i < count; i++)
                    items[i] = new ScriptItem { Code = $"Console.WriteLine(\"iso-{i}\");" };

                var sessions = items.Select(item => runtime.GetSession(item)).ToArray();
                var results = await Task.WhenAll(sessions.Select(s => s.RunAsync()));

                for (int i = 0; i < count; i++)
                {
                    ctx.Assert(results[i], $"脚本 {i} 执行失败: {sessions[i].StatusText}");
                    var output = sessions[i].Output;
                    ctx.Assert(output.Contains($"iso-{i}", StringComparison.Ordinal),
                        $"脚本 {i} 输出缺失: {output}");
                    for (int j = 0; j < count; j++)
                    {
                        if (i == j) continue;
                        ctx.Assert(!output.Contains($"iso-{j}", StringComparison.Ordinal),
                            $"脚本 {i} 输出串扰了脚本 {j} 的内容: {output}");
                    }
                }
            }
            finally
            {
                foreach (var item in items) runtime.ReleaseSession(item);
            }
        });

        runner.Register(Cat, "并行-循环标志与运行标志相互独立", async ctx =>
        {
            var looping = new ScriptItem { Code = "Console.WriteLine(\"loop-only\");" };
            var once = new ScriptItem { Code = "Console.WriteLine(\"once-only\");" };
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                var loopSession = runtime.GetSession(looping);
                loopSession.LoopInterval = 50;
                var loopTask = loopSession.LoopRunAsync();
                await Task.Delay(500);

                var onceSession = runtime.GetSession(once);
                var onceOk = await onceSession.RunAsync();

                ctx.Assert(onceOk, "单次执行应成功");
                ctx.Assert(loopSession.IsLooping, "循环会话应处于循环态");
                ctx.Assert(!onceSession.IsLooping, "单次会话不应被置为循环态");
                ctx.Assert(!onceSession.IsRunning, "单次会话执行结束后不应仍在运行");
                ctx.Assert(loopSession.Output.Contains("loop-only", StringComparison.Ordinal),
                    "循环会话输出缺失");
                ctx.Assert(!loopSession.Output.Contains("once-only", StringComparison.Ordinal),
                    "循环会话不应混入单次会话输出");

                loopSession.StopLoop();
                var completed = await Task.WhenAny(loopTask, Task.Delay(5000)) == loopTask;
                ctx.Assert(completed, "StopLoop 后循环应在 5 秒内结束");
                ctx.Assert(!loopSession.IsLooping, "停止后循环标志应复位");
            }
            finally
            {
                runtime.ReleaseSession(looping);
                runtime.ReleaseSession(once);
            }
        });
    }

    // ── 生命周期 ──────────────────────────────────────────
    private static void RegisterLifecycle(TestRunner runner)
    {
        runner.Register(Cat, "生命周期-释放会话后重建为全新状态", async ctx =>
        {
            var item = new ScriptItem { Code = "Console.WriteLine(\"before-release\");" };
            var runtime = RoslynScriptRuntime.Shared;
            try
            {
                var session = runtime.GetSession(item);
                await session.RunAsync();
                ctx.Assert(session.Output.Contains("before-release", StringComparison.Ordinal),
                    "释放前应有输出");

                runtime.ReleaseSession(item);
                ctx.Assert(runtime.FindSession(item) == null, "释放后不应再存在关联会话");

                var rebuilt = runtime.GetSession(item);
                ctx.Assert(!ReferenceEquals(rebuilt, session), "释放后重建应为新会话实例");
                ctx.Assert(rebuilt.Output.Length == 0, $"新会话输出应为空，实际={rebuilt.Output}");
                ctx.Assert(rebuilt.CompilationState == ScriptCompilationState.NotCompiled,
                    "新会话应回到 NotCompiled");
            }
            finally { runtime.ReleaseSession(item); }
        });

        runner.Register(Cat, "生命周期-无头执行不残留会话关联", async ctx =>
        {
            var projectItem = new ScriptProjectItem
            {
                Title = "headless",
                Code = "Console.WriteLine(\"headless-p3\");"
            };
            var runtime = RoslynScriptRuntime.Shared;

            var result = await runtime.ExecuteAsync(projectItem);
            ctx.Assert(result.Success, $"无头执行失败: {result.Status}");
            ctx.Assert(result.Output.Contains("headless-p3", StringComparison.Ordinal),
                $"无头执行输出不正确: {result.Output}");

            // 无头执行使用一次性会话：不应在运行时上留下可长期复用的关联
            ctx.Assert(runtime.FindSession(new ScriptItem()) == null, "普通脚本对象不应凭空获得会话");
        });
    }
}
