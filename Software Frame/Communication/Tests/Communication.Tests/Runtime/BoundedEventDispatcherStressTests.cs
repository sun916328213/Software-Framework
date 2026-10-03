// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：BoundedEventDispatcherStressTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、System.Diagnostics.Metrics、Communication.Runtime、Xunit。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Communication.Runtime;
using Xunit;

namespace Communication.Tests.Runtime;

/// <summary>
/// BoundedEventDispatcherStressTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class BoundedEventDispatcherStressTests
{
    /// <summary>
    /// 验证 Full_queue_keeps_latest_items_in_order_and_reports_every_drop 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewGuid、CreateDropListener、Add、Start、Enqueue；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Full_queue_keeps_latest_items_in_order_and_reports_every_drop()
    {
        const int capacity = 8;
        const int publishedWhileBlocked = 10_000;
        var protocolTag = $"stress-{Guid.NewGuid():N}";
        var context = new CommunicationDiagnosticContext(protocolTag, "memory");
        var received = new ConcurrentQueue<int>();
        var handlerEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseHandler = new ManualResetEventSlim();
        long dropped = 0;
        using var meterListener = CreateDropListener(
            protocolTag,
            value => Interlocked.Add(ref dropped, value));
        meterListener.Start();

        var dispatcher = new BoundedEventDispatcher<int>(
            capacity,
            item =>
            {
                received.Enqueue(item);
                if (item == 0)
                {
                    handlerEntered.TrySetResult();
                    releaseHandler.Wait();
                }
            },
            _ => { },
            "stress");
        Assert.True(dispatcher.TryPublish(0, context));
        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        for (var item = 1; item <= publishedWhileBlocked; item++)
        {
            Assert.True(dispatcher.TryPublish(item, context));
        }

        releaseHandler.Set();
        await dispatcher.DisposeAsync();

        Assert.Equal(
            new[] { 0 }.Concat(Enumerable.Range(
                publishedWhileBlocked - capacity + 1,
                capacity)),
            received);
        Assert.Equal(publishedWhileBlocked - capacity, Volatile.Read(ref dropped));
        Assert.False(dispatcher.TryPublish(publishedWhileBlocked + 1, context));
    }

    /// <summary>
    /// 验证 CreateDropListener 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 EnableMeasurementEvents、SetMeasurementEventCallback、Equals、recordDrop；具体条件和先后次序见方法体。
    /// </summary>
    private static MeterListener CreateDropListener(
        string protocolTag,
        Action<long> recordDrop)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.events.dropped")
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "protocol" &&
                    string.Equals(tag.Value?.ToString(), protocolTag, StringComparison.Ordinal))
                {
                    recordDrop(value);
                    break;
                }
            }
        });
        return listener;
    }
}
