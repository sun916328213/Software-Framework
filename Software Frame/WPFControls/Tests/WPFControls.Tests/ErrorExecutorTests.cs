// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ErrorExecutorTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.DryIoc、System.IO、Prism.Ioc、WPFControls.Controls.Diagnostics、Xunit。导入不代表所有类型都会在运行时加载。
using Prism.DryIoc;
using System.IO;
using Prism.Ioc;
using WPFControls.Controls.Diagnostics;
using Xunit;

namespace WPFControls.Tests;

/// <summary>
/// ErrorExecutorTests 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ErrorExecutorTests
{
    /// <summary>
    /// 测试相关入口 ContainersKeepIndependentErrorTargets；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterSingleton、TryExecute、Resolve、Equal、Select；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ContainersKeepIndependentErrorTargets()
    {
        var firstErrors = new List<Exception>();
        var secondErrors = new List<Exception>();
        var first = new DryIocContainerExtension();
        var second = new DryIocContainerExtension();
        first.RegisterSingleton<TryCatchHelper>(() => new TryCatchHelper(firstErrors.Add));
        second.RegisterSingleton<TryCatchHelper>(() => new TryCatchHelper(secondErrors.Add));
        first.Resolve<TryCatchHelper>().TryExecute(() => throw new InvalidOperationException("first"));
        second.Resolve<TryCatchHelper>().TryExecute(() => throw new InvalidOperationException("second"));
        first.Resolve<TryCatchHelper>().TryExecute(() => throw new InvalidOperationException("first again"));
        Assert.Equal(new[] { "first", "first again" }, firstErrors.Select(error => error.Message));
        Assert.Equal("second", Assert.Single(secondErrors).Message);
    }

    /// <summary>
    /// 测试相关入口 ReentrantAndThrowingReporterCannotEscapeOrLoop；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 TryExecute、False、TryExecuteWithResult、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ReentrantAndThrowingReporterCannotEscapeOrLoop()
    {
        var reports = 0;
        TryCatchHelper? executor = null;
        executor = new TryCatchHelper(error =>
        {
            reports++;
            executor!.TryExecute(() => throw new InvalidOperationException("nested"));
            throw new IOException("report failed");
        });
        Assert.False(executor.TryExecuteWithResult(() => throw new InvalidOperationException("operation")));
        Assert.Equal(1, reports);
        Assert.False(executor.TryExecuteWithResult(() => throw new InvalidOperationException("next")));
        Assert.Equal(2, reports);
    }

    /// <summary>
    /// 测试相关入口 ThrowingErrorCallbackStillReportsOriginalFailure；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 TryExecute、Same、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ThrowingErrorCallbackStillReportsOriginalFailure()
    {
        var reported = new List<Exception>();
        var error = new InvalidOperationException("original");
        new TryCatchHelper(reported.Add).TryExecute(() => throw error, _ => throw new IOException("callback"));
        Assert.Same(error, Assert.Single(reported));
    }

    /// <summary>
    /// 测试相关入口 AsyncFailureReportsAndReturnsCallerFallback；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 TryExecuteAsync、Yield、Equal、Single、FromResult；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task AsyncFailureReportsAndReturnsCallerFallback()
    {
        var reported = new List<Exception>();
        var executor = new TryCatchHelper(reported.Add);
        var result = await executor.TryExecuteAsync<int>(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("async");
        }, -1);
        Assert.Equal(-1, result);
        Assert.Equal("async", Assert.Single(reported).Message);
        Assert.Equal(42, await executor.TryExecuteAsync(() => Task.FromResult(42)));
    }

    /// <summary>
    /// 测试相关入口 LoggingFailureDoesNotCallItsOwnReporter；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 TryExecuteWithoutReport、Equal、TryExecute；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoggingFailureDoesNotCallItsOwnReporter()
    {
        var reports = 0;
        var executor = new TryCatchHelper(_ => reports++);
        executor.TryExecuteWithoutReport(() => throw new IOException("log unavailable"));
        Assert.Equal(0, reports);
        executor.TryExecute(() => throw new InvalidOperationException("operation"));
        Assert.Equal(1, reports);
    }
}
