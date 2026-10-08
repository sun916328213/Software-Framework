// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PerformanceThresholds。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Core.Models、ScriptEditor.Runtime.Services、Xunit、Xunit.Abstractions。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Core.Models;
using ScriptEditor.Runtime.Services;
using Xunit;
using Xunit.Abstractions;

namespace ScriptEditor.Tests.Performance;

/// <summary>
/// 性能回归阈值（P6）：重复编译、预编译加速、输出洪泛、批量任务、长期循环、内存稳定。
/// 阈值为 CI 回归线（留足余量），非精密基准；Workbench 负责人工性能观察。
/// <para>
/// Category 隔离：本类用例耗时远高于纯逻辑用例（循环 2s、批量预编译以分钟计），
/// 按 <c>ARCHITECTURE.md §2</c>「长时间运行测试用 Category 隔离，不混入快速单元测试」归类为
/// <c>Performance</c>。快速线用 <c>--filter "Category!=Performance"</c> 排除；
/// 与 Vision 的 <c>Performance</c>（只记录、不设阈值）口径不同：此处保留 CI 回归阈值。
/// </para>
/// </summary>
[Trait("Category", "Performance")]
public sealed class PerformanceThresholds : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<ScriptItem> _items = new();
    private readonly List<ScriptSession> _detached = new();

    /// <summary>
    /// 初始化 PerformanceThresholds 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PerformanceThresholds(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ReleaseSession；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        foreach (var item in _items)
            RoslynScriptRuntime.Shared.ReleaseSession(item);
        foreach (var session in _detached)
            RoslynScriptRuntime.Shared.ReleaseSession(session);
    }

    /// <summary>
    /// 测试相关入口 NewDetached；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
    /// </summary>
    private ScriptSession NewDetached(string code)
    {
        var session = new ScriptSession(() => code);
        _detached.Add(session);
        return session;
    }

    /// <summary>
    /// 测试相关入口 PrecompiledFirstRunIsFast；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、PreCompileAsync、StartNew、True、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 OutputFlood1000Lines；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、StartNew、True、RunAsync、Stop；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 TwentyScriptTaskPrecompiles；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、GetSession、StartNew、WhenAll、Select；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 LongLoopRunIsStable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、LoopRunAsync、Delay、StopLoop、WhenAny；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 RepeatedRecompilesStayStable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、True、RunAsync、Max、Collect；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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
