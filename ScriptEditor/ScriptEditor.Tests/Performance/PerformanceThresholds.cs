using ScriptEditor.Models;
using ScriptEditor.Services;
using Xunit;
using Xunit.Abstractions;

namespace ScriptEditor.Tests.Performance;

/// <summary>
/// 性能回归阈值（P6）：重复编译、预编译加速、输出洪泛、批量任务、长期循环、内存稳定。
/// 阈值为 CI 回归线（留足余量），非精密基准；Workbench 负责人工性能观察。
/// </summary>
public sealed class PerformanceThresholds : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<ScriptItem> _items = new();
    private readonly List<ScriptSession> _detached = new();

    public PerformanceThresholds(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        foreach (var item in _items)
            RoslynScriptRuntime.Shared.ReleaseSession(item);
        foreach (var session in _detached)
            RoslynScriptRuntime.Shared.ReleaseSession(session);
    }

    private ScriptSession NewDetached(string code)
    {
        var session = new ScriptSession(() => code);
        _detached.Add(session);
        return session;
    }

    [Fact]
    public async Task PrecompiledFirstRunIsFast()
    {
        var session = NewDetached("Console.WriteLine(\"pre\");");
        await session.PreCompileAsync("Console.WriteLine(\"pre\");");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(await session.RunAsync());
        sw.Stop();
        _output.WriteLine($"precompiled first run: {sw.Elapsed.TotalMilliseconds:F0} ms");
        Assert.True(sw.Elapsed.TotalMilliseconds < 5000, $"预编译后首次执行过慢: {sw.Elapsed.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public async Task OutputFlood1000Lines()
    {
        var session = NewDetached("for (int i = 0; i < 1000; i++) Console.WriteLine($\"line-{i}\");");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(await session.RunAsync());
        sw.Stop();
        session.FlushOutput();
        _output.WriteLine($"flood 1000 lines: {sw.Elapsed.TotalMilliseconds:F0} ms");
        Assert.True(sw.Elapsed.TotalMilliseconds < 20000, $"1000 行输出耗时过长: {sw.Elapsed.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public async Task TwentyScriptTaskPrecompiles()
    {
        const int count = 20;
        var runtime = RoslynScriptRuntime.Shared;
        var sessions = new List<ScriptSession>(count);
        for (var i = 0; i < count; i++)
        {
            var item = new ScriptItem
            {
                Title = $"perf-{i}",
                Code = $"Console.WriteLine(\"task-{i}\");"
            };
            _items.Add(item);
            sessions.Add((ScriptSession)runtime.GetSession(item));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Task.WhenAll(_items.Select(item =>
            ((ScriptSession)runtime.GetSession(item)).PreCompileAsync(item.Code)));
        sw.Stop();
        _output.WriteLine($"20 sessions precompile: {sw.Elapsed.TotalMilliseconds:F0} ms");
        Assert.True(sw.Elapsed.TotalMilliseconds < 120000, $"20 脚本预编译过慢: {sw.Elapsed.TotalMilliseconds:F0} ms");

        // 抽查：预编译后执行成功。
        _items[0].Code = "Console.WriteLine(\"task-0\");";
        Assert.True(await sessions[0].RunAsync());
        Assert.Contains("task-0", sessions[0].Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LongLoopRunIsStable()
    {
        var session = NewDetached("Console.WriteLine(\"tick\");");
        session.LoopInterval = 20;
        var loopTask = session.LoopRunAsync();
        await Task.Delay(2000);
        session.StopLoop();
        var completed = await Task.WhenAny(loopTask, Task.Delay(10000)) == loopTask;
        Assert.True(completed, "长时间循环停止超时");
        Assert.False(session.IsLooping);
        Assert.Contains("tick", session.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepeatedRecompilesStayStable()
    {
        const int rounds = 40;
        long peak = 0;
        for (var i = 0; i < rounds; i++)
        {
            var index = i;
            var session = NewDetached($"Console.WriteLine(\"mem-round-{index}\");");
            Assert.True(await session.RunAsync());
            peak = Math.Max(peak, Environment.WorkingSet);
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var after = Environment.WorkingSet;
        _output.WriteLine($"peak={peak / 1048576:F1} MB after={after / 1048576:F1} MB");
        Assert.True(after < peak * 1.5, $"GC 后工作集异常增长: {peak / 1048576:F1} MB -> {after / 1048576:F1} MB");
    }
}
