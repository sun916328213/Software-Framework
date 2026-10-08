// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ScriptSessionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Core.Models、ScriptEditor.Core.Options、ScriptEditor.Runtime.Services、Xunit。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Core.Models;
using ScriptEditor.Core.Options;
using ScriptEditor.Runtime.Services;
using Xunit;

namespace ScriptEditor.Tests.Runtime;

/// <summary>
/// 会话测试：编译诊断、重复执行、取消/终止、循环停止、并发隔离、任务上下文（无 WPF）。
/// </summary>
public sealed class ScriptSessionTests : IDisposable
{
    private readonly List<ScriptItem> _items = new();
    private readonly List<ScriptSession> _detached = new();

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
    /// 测试相关入口 NewItem；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToString、NewGuid、Add；具体条件和先后次序见方法体。
    /// </summary>
    private ScriptItem NewItem(string code)
    {
        var item = new ScriptItem { Title = Guid.NewGuid().ToString("N"), Code = code };
        _items.Add(item);
        return item;
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
    /// 测试相关入口 CompileSuccessMarksStatus；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、CompileAsync、Equal、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CompileSuccessMarksStatus()
    {
        var session = NewDetached("Console.WriteLine(\"ok\");");
        await session.CompileAsync();
        Assert.Equal("编译完成", session.StatusText);
        Assert.Contains("✓", session.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 CompileErrorReportsLine；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、CompileAsync、Equal、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CompileErrorReportsLine()
    {
        var session = NewDetached("var x = ;");
        await session.CompileAsync();
        Assert.Equal("编译失败", session.StatusText);
        Assert.Contains("行", session.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 FailedCompileDoesNotPoisonCache；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、True、RunAsync、Add、CompileAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task FailedCompileDoesNotPoisonCache()
    {
        var code = "Console.WriteLine(\"v1\");";
        var session = NewDetached(code);
        Assert.True(await session.RunAsync());

        var mutable = code;
        var session2 = new ScriptSession(() => mutable);
        _detached.Add(session2);
        await session2.CompileAsync();
        mutable = "var y = ;";
        await session2.CompileAsync();
        Assert.Equal("编译失败", session2.StatusText);
        mutable = code;
        Assert.True(await session2.RunAsync());
    }

    /// <summary>
    /// 测试相关入口 RunCapturesConsoleOutput；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、RunAsync、True、Contains、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RunCapturesConsoleOutput()
    {
        var session = NewDetached("Console.WriteLine(\"hello-core\");");
        var ok = await session.RunAsync();
        Assert.True(ok, session.StatusText);
        Assert.Contains("hello-core", session.Output, StringComparison.Ordinal);
        Assert.Equal("执行成功", session.StatusText);
    }

    /// <summary>
    /// 测试相关入口 RepeatedExecutionReusesCache；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、True、RunAsync、Contains、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RepeatedExecutionReusesCache()
    {
        var session = NewDetached("Console.WriteLine(\"repeat\");");
        Assert.True(await session.RunAsync());
        Assert.Contains("repeat", session.Output, StringComparison.Ordinal);
        // 缓存命中：相同代码再次执行成功且输出一致（时间戳除外）。
        Assert.True(await session.RunAsync());
        Assert.Contains("repeat", session.Output, StringComparison.Ordinal);
        Assert.Equal("执行成功", session.StatusText);
    }

    /// <summary>
    /// 测试相关入口 InputsDictionaryInjection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、SetInputData、RunAsync、True、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task InputsDictionaryInjection()
    {
        var session = NewDetached("Console.WriteLine($\"a={Inputs[\"k\"]}\");");
        session.SetInputData(new Dictionary<string, object?> { ["k"] = 77 });
        var ok = await session.RunAsync();
        Assert.True(ok, $"{session.StatusText}/{session.Output}");
        Assert.Contains("a=77", session.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 RuntimeErrorMarksFailure；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、RunAsync、False、Equal、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task RuntimeErrorMarksFailure()
    {
        var session = NewDetached("throw new InvalidOperationException(\"boom\");");
        var ok = await session.RunAsync();
        Assert.False(ok);
        Assert.Equal("执行失败", session.StatusText);
        Assert.Contains("boom", session.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 EmptyCodeHintsFriendly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、RunAsync、False、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task EmptyCodeHintsFriendly()
    {
        var session = NewDetached(string.Empty);
        var ok = await session.RunAsync();
        Assert.False(ok);
        Assert.Contains("请输入", session.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 BreakpointInstrumentationStillRuns；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、RunAsync、True、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task BreakpointInstrumentationStillRuns()
    {
        var session = new ScriptSession(
            () => "Console.WriteLine(\"bp-line\");",
            getBreakpoints: () => new[] { 1 });
        _detached.Add(session);
        var ok = await session.RunAsync();
        Assert.True(ok, $"{session.StatusText}/{session.Output}");
        Assert.Contains("bp-line", session.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 TerminateIsResponsiveAndSessionReusable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、RunAsync、Delay、Terminate、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task TerminateIsResponsiveAndSessionReusable()
    {
        var code = "while (true) { System.Threading.Thread.Sleep(100); }";
        var session = NewDetached(code);
        _ = session.RunAsync();
        await Task.Delay(800);
        session.Terminate();
        Assert.False(session.IsRunning);

        // 终止无残留污染：新会话可正常执行。
        var session2 = NewDetached("Console.WriteLine(\"reuse-ok\");");
        Assert.True(await session2.RunAsync());
        Assert.Contains("reuse-ok", session2.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 LoopStopsWithinTimeout；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewDetached、LoopRunAsync、Delay、StopLoop、WhenAny；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LoopStopsWithinTimeout()
    {
        var session = NewDetached("Console.WriteLine(\"tick\");");
        session.LoopInterval = 50;
        var loopTask = session.LoopRunAsync();
        await Task.Delay(700);
        session.StopLoop();
        var completed = await Task.WhenAny(loopTask, Task.Delay(5000)) == loopTask;
        Assert.True(completed, "StopLoop 后 5 秒内未结束");
        Assert.False(session.IsLooping);
        Assert.False(session.IsRunning);
        Assert.Contains("tick", session.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 ParallelSessionsDoNotCrossTalk；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewItem、GetSession、SetInputData、WhenAll、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ParallelSessionsDoNotCrossTalk()
    {
        var itemA = NewItem("Console.WriteLine($\"v={Inputs[\"k\"]}\");");
        var itemB = NewItem("Console.WriteLine($\"v={Inputs[\"k\"]}\");");
        var runtime = RoslynScriptRuntime.Shared;
        var sessionA = runtime.GetSession(itemA);
        var sessionB = runtime.GetSession(itemB);
        sessionA.SetInputData(new Dictionary<string, object?> { ["k"] = 1 });
        sessionB.SetInputData(new Dictionary<string, object?> { ["k"] = 2 });

        var results = await Task.WhenAll(sessionA.RunAsync(), sessionB.RunAsync());
        Assert.All(results, Assert.True);
        Assert.Contains("v=1", sessionA.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("v=2", sessionA.Output, StringComparison.Ordinal);
        Assert.Contains("v=2", sessionB.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("v=1", sessionB.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 SameItemReusesSession；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewItem、Same、GetSession；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SameItemReusesSession()
    {
        var item = NewItem("Console.WriteLine(1);");
        var runtime = RoslynScriptRuntime.Shared;
        Assert.Same(runtime.GetSession(item), runtime.GetSession(item));
    }

    /// <summary>
    /// 测试相关入口 ProjectContextUsingsTakeEffect；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddSources、NewDetached、RunAsync、True、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ProjectContextUsingsTakeEffect()
    {
        var context = new ScriptProjectContext { Name = "ctx-run" };
        context.AddSources(new[] { "using System.Text;\n" });
        var session = NewDetached(
            "var sb = new StringBuilder(); sb.Append(\"ctx-ok\"); Console.WriteLine(sb.ToString());");
        session.ProjectContext = context;
        var ok = await session.RunAsync();
        Assert.True(ok, $"{session.StatusText}/{session.Output}");
        Assert.Contains("ctx-ok", session.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 ProjectContextsDoNotPolluteEachOther；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddSources、ResolveUsings、Contains、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProjectContextsDoNotPolluteEachOther()
    {
        var contextA = new ScriptProjectContext { Name = "A" };
        var contextB = new ScriptProjectContext { Name = "B" };
        contextA.AddSources(new[] { "using System.Text;\nConsole.WriteLine(1);" });
        contextB.AddSources(new[] { "using System.IO;\nConsole.WriteLine(2);" });
        var a = contextA.ResolveUsings();
        var b = contextB.ResolveUsings();
        Assert.Contains("System.Text", a);
        Assert.DoesNotContain("System.IO", a);
        Assert.Contains("System.IO", b);
        Assert.DoesNotContain("System.Text", b);
    }

    /// <summary>
    /// 测试相关入口 HeadlessRuntimeApiExecutesProjectItem；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、True、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task HeadlessRuntimeApiExecutesProjectItem()
    {
        var script = new ScriptProjectItem
        {
            Title = "api",
            Code = "Console.WriteLine(\"api-ok\");"
        };
        var result = await RoslynScriptRuntime.Shared.ExecuteAsync(script);
        Assert.True(result.Success, result.Status);
        Assert.Contains("api-ok", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 HeadlessRuntimeApiCompilesProjectItem；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 False、CompileAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task HeadlessRuntimeApiCompilesProjectItem()
    {
        var script = new ScriptProjectItem { Title = "api-c", Code = "var x = ;" };
        Assert.False(await RoslynScriptRuntime.Shared.CompileAsync(script));
    }
}
