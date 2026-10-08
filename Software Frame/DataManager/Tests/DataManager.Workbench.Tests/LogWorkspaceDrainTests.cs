// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LogWorkspaceDrainTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls.Services、DataManager.Controls.Configuration、DataManager.Core.Log、DataManager.Core.Log.Definition、System.Reflection。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using DataManager.Controls.Configuration;
using DataManager.Controls.Services;
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;

namespace DataManager.Workbench.Tests;

/// <summary>
/// T08：<see cref="LogWorkspace"/> 的「慢查询 / 指纹重建 / 关闭」生命周期回归。
/// 与 <see cref="SqlWorkspaceDrainTests"/> 同型——用可阻塞的 <see cref="ILog"/>、<see cref="ILogManager"/>
/// 代理制造「非协作式慢操作」，断言四件事：
/// <list type="number">
/// <item>释放先取消再排空：在途操作没结束前释放不返回，且同一实例只被摘除/释放一次；</item>
/// <item>异步释放不阻塞调用方（宿主关闭路径靠这条不卡住 UI 线程）；</item>
/// <item>指纹重建会取消在途查询，旧结果不再回写 <c>Model.Results</c>；</item>
/// <item>关闭后写入/查询/重建一律 <see cref="ObjectDisposedException"/>，不再触碰日志实例。</item>
/// </list>
/// </summary>
public class LogWorkspaceDrainTests
{
    /// <summary>
    /// 生产里三种日志实现（SerilogHelper / NLogHelper / Log4NetHelper）都实现了 <see cref="IDisposable"/>，
    /// 但 <see cref="ILog"/> 契约本身不含 Dispose——Workspace 用 <c>as IDisposable</c> 探测后释放。
    /// 这里显式声明「可释放的 ILog」，才能断言释放确实被调用到。
    /// </summary>
    public interface ILogWithDispose : ILog, IDisposable
    {
    }

    /// <summary>造一个已登记 <c>SelectedFile</c> 的隔离工作区（配置目录不存在 ⇒ 回退到默认档案）。</summary>
    private static LogWorkspace CreateWorkspace(ILogManager manager)
    {
        var workspace = new LogWorkspace(manager, ConfigRoot.CreateTemporary());
        workspace.Model.SelectedFile = "log20261008";
        return workspace;
    }

    /// <summary>
    /// 在途查询未结束时释放：释放必须等它让出门闩；两个并发释放只摘除/释放一次；
    /// 无论旧查询是成功还是抛错，关闭后都不再回写结果集。
    /// </summary>
    /// <param name="fail">旧查询以异常收场（验证排空过程不吞掉调用方的失败）。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncDisposalDrainsNonCooperativeQueryAndSurvivesFailure(bool fail)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var removes = 0;
        var disposals = 0;
        var log = Proxy<ILogWithDispose>(method => method.Name switch
        {
            "Query" => SlowQuery(),
            "Dispose" => DisposeLog(),
            _ => throw new NotSupportedException(method.Name),
        });
        object? SlowQuery()
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            if (fail) throw new IOException("provider-query-fault");
            return new List<LogInfo> { new("t", "late") };
        }
        object? DisposeLog()
        {
            Interlocked.Increment(ref disposals);
            return null;
        }

        var manager = Proxy<ILogManager>(method => method.Name switch
        {
            "Create" => log,
            "Remove" => RemoveLog(),
            _ => throw new NotSupportedException(method.Name),
        });
        object? RemoveLog()
        {
            Interlocked.Increment(ref removes);
            return true;
        }

        var workspace = CreateWorkspace(manager);
        var pending = workspace.QueryByFileAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var first = workspace.DisposeAsync().AsTask();
        var second = workspace.DisposeAsync().AsTask();
        try
        {
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            Assert.Equal(0, removes);
            Assert.Equal(0, disposals);
        }
        finally
        {
            release.Set();
            if (fail) await Assert.ThrowsAsync<IOException>(() => pending);
            else await pending.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(1, removes);
        Assert.Equal(1, disposals);
        Assert.Empty(workspace.Model.Results);
    }

    /// <summary>
    /// 异步释放本身不阻塞调用方：调用立刻返回，排空在返回的 Task 上继续。
    /// 宿主关闭路径（WorkbenchLifetime → DisposeAsync）依赖这条才不至于把 UI 线程钉在慢查询上。
    /// </summary>
    [Fact]
    public async Task AsyncDisposalReturnsWhileNonCooperativeQueryIsPending()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var log = Proxy<ILogWithDispose>(method => method.Name switch
        {
            "Query" => SlowQuery(),
            "Dispose" => null,
            _ => throw new NotSupportedException(method.Name),
        });
        object? SlowQuery()
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return new List<LogInfo>();
        }

        var workspace = CreateWorkspace(Proxy<ILogManager>(method => method.Name switch
        {
            "Create" => log,
            "Remove" => true,
            _ => throw new NotSupportedException(method.Name),
        }));
        var pending = workspace.QueryByFileAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var close = Task.Run(async () =>
        {
            var disposing = workspace.DisposeAsync();
            returned.TrySetResult();
            await disposing;
        });
        try
        {
            Assert.Same(returned.Task, await Task.WhenAny(returned.Task, Task.Delay(1000)));
        }
        finally
        {
            release.Set();
            await close.WaitAsync(TimeSpan.FromSeconds(10));
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Empty(workspace.Model.Results);
    }

    /// <summary>
    /// 指纹重建（切换档案 / 改配置）要取消在途查询：重建先等旧操作让出门闩，
    /// 旧查询晚到的结果不再覆盖界面——这是改造前「只 Dispose 不取消」的缺陷点。
    /// </summary>
    [Fact]
    public async Task FingerprintChangeCancelsInFlightQueryAndDiscardsLateResult()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var creates = 0;
        var log = Proxy<ILogWithDispose>(method => method.Name switch
        {
            "Query" => SlowQuery(),
            "Dispose" => null,
            _ => throw new NotSupportedException(method.Name),
        });
        object? SlowQuery()
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return new List<LogInfo> { new("t", "late") };
        }

        var manager = Proxy<ILogManager>(method => method.Name switch
        {
            "Create" => Create(),
            "Remove" => true,
            _ => throw new NotSupportedException(method.Name),
        });
        object? Create()
        {
            Interlocked.Increment(ref creates);
            return log;
        }

        var workspace = CreateWorkspace(manager);
        var pending = workspace.QueryByFileAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var rebuild = Task.Run(() => workspace.EnsureLog(new LogParam { Name = "other", FilePath = @"C:\logs2" }));
        try
        {
            // 重建要等旧查询让出门闩，说明它确实在排空在途操作，而不是并行重建。
            Assert.NotSame(rebuild, await Task.WhenAny(rebuild, Task.Delay(300)));
        }
        finally
        {
            release.Set();
            await rebuild.WaitAsync(TimeSpan.FromSeconds(10));
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(2, creates);
        Assert.Empty(workspace.Model.Results);
    }

    /// <summary>
    /// 批量写入在取消后立刻停手：第 2 条循环即观察到取消，不再继续调用 Provider。
    /// </summary>
    [Fact]
    public async Task WriteManyStopsAtNextIterationWhenClosed()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var adds = 0;
        var removes = 0;
        var disposals = 0;
        var log = Proxy<ILogWithDispose>(method => method.Name switch
        {
            "AddAsync" => SlowAdd(),
            "Dispose" => DisposeLog(),
            _ => throw new NotSupportedException(method.Name),
        });
        Task SlowAdd()
        {
            Interlocked.Increment(ref adds);
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return Task.CompletedTask;
        }
        object? DisposeLog()
        {
            Interlocked.Increment(ref disposals);
            return null;
        }

        var manager = Proxy<ILogManager>(method => method.Name switch
        {
            "Create" => log,
            "Remove" => RemoveLog(),
            _ => throw new NotSupportedException(method.Name),
        });
        object? RemoveLog()
        {
            Interlocked.Increment(ref removes);
            return true;
        }

        var workspace = CreateWorkspace(manager);
        var pending = workspace.WriteManyAsync(count: 3);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var disposing = workspace.DisposeAsync().AsTask();
        try
        {
            Assert.False(disposing.IsCompleted);
        }
        finally
        {
            release.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            await disposing.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(1, adds);
        Assert.Equal(1, removes);
        Assert.Equal(1, disposals);
    }

    /// <summary>
    /// 未取消的查询照常回填结果集：T08 重写了查询路径，这条守住「正常路径没被改坏」。
    /// </summary>
    [Fact]
    public async Task QueryByFileStillDeliversResultsWhenNotCancelled()
    {
        var log = Proxy<ILogWithDispose>(method => method.Name switch
        {
            "Query" => QueryResult(),
            "Dispose" => null,
            _ => throw new NotSupportedException(method.Name),
        });
        object? QueryResult() => new List<LogInfo> { new("t1", "hello") };

        var workspace = CreateWorkspace(Proxy<ILogManager>(method => method.Name switch
        {
            "Create" => log,
            "Remove" => true,
            _ => throw new NotSupportedException(method.Name),
        }));

        await workspace.QueryByFileAsync();

        var result = Assert.Single(workspace.Model.Results);
        Assert.Equal("hello", result.Info);
        await workspace.DisposeAsync();
    }

    /// <summary>
    /// 关闭后写入、查询、重建都不再触碰日志实例：统一抛 <see cref="ObjectDisposedException"/>。
    /// </summary>
    [Fact]
    public async Task OperationsAfterCloseThrowObjectDisposed()
    {
        var workspace = CreateWorkspace(Proxy<ILogManager>(method => method.Name switch
        {
            "Create" => Proxy<ILogWithDispose>(_ => null),
            "Remove" => true,
            _ => throw new NotSupportedException(method.Name),
        }));
        await workspace.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => workspace.QueryByFileAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => workspace.QueryByRangeAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => workspace.QueryMultiAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => workspace.WriteAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => workspace.WriteManyAsync());
        Assert.Throws<ObjectDisposedException>(() => workspace.ResetLog(new LogParam { Name = "x", FilePath = @"C:\x" }));
        Assert.Throws<ObjectDisposedException>(() => workspace.EnsureLog(new LogParam { Name = "x", FilePath = @"C:\x" }));
    }

    private static T Proxy<T>(Func<MethodInfo, object?> action) where T : class
    {
        var proxy = DispatchProxy.Create<T, ProviderProxy>();
        ((ProviderProxy)(object)proxy).Action = action;
        return proxy;
    }

    public class ProviderProxy : DispatchProxy
    {
        public Func<MethodInfo, object?> Action { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Action(targetMethod!);
    }
}
