// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：BoundedOperationTrackerTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Runtime、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Runtime;
using Xunit;

namespace Communication.Tests.Runtime;

/// <summary>
/// BoundedOperationTrackerTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class BoundedOperationTrackerTests
{
    /// <summary>
    /// 验证 Tracker_rejects_work_above_capacity_and_accepts_again_after_completion 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 True、TryStart、False、Equal、SetResult；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Tracker_rejects_work_above_capacity_and_accepts_again_after_completion()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tracker = new BoundedOperationTracker(2, _ => { });

        Assert.True(tracker.TryStart(() => release.Task));
        Assert.True(tracker.TryStart(() => release.Task));
        Assert.False(tracker.TryStart(() => Task.CompletedTask));
        Assert.Equal(2, tracker.Count);

        release.SetResult();
        await tracker.DrainAsync();

        Assert.Equal(0, tracker.Count);
        Assert.True(tracker.TryStart(() => Task.CompletedTask));
        await tracker.DrainAsync();
        Assert.Equal(0, tracker.Count);
    }

    /// <summary>
    /// 验证 Tracker_reports_operation_failure_and_still_releases_capacity 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 True、TryStart、FromException、DrainAsync、IsType；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Tracker_reports_operation_failure_and_still_releases_capacity()
    {
        Exception? reported = null;
        var tracker = new BoundedOperationTracker(1, exception => reported = exception);

        Assert.True(tracker.TryStart(() => Task.FromException(new InvalidOperationException("boom"))));
        await tracker.DrainAsync();

        Assert.IsType<InvalidOperationException>(reported);
        Assert.Equal(0, tracker.Count);
        Assert.True(tracker.TryStart(() => Task.CompletedTask));
        await tracker.DrainAsync();
    }
}
