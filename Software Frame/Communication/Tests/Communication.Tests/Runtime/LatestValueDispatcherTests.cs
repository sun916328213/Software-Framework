// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LatestValueDispatcherTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、System.Diagnostics.Metrics、Communication.Runtime、Xunit。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Communication.Runtime;
using Xunit;

namespace Communication.Tests.Runtime;

/// <summary>
/// LatestValueDispatcherTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class LatestValueDispatcherTests
{
    /// <summary>
    /// 验证 Blocked_consumer_receives_only_the_latest_pending_value_for_each_key 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 EnableMeasurementEvents、SetMeasurementEventCallback、Equals、Add、Start；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Blocked_consumer_receives_only_the_latest_pending_value_for_each_key()
    {
        long coalesced = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.events.coalesced")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "protocol" && Equals(tag.Value, "test"))
                {
                    Interlocked.Add(ref coalesced, value);
                    break;
                }
            }
        });
        listener.Start();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = new ConcurrentQueue<int>();
        await using var dispatcher = new LatestValueDispatcher<string, int>(
            capacity: 8,
            item =>
            {
                delivered.Enqueue(item);
                if (item == 1)
                {
                    entered.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                }
            },
            _ => { },
            "data_point_changed",
            new CommunicationDiagnosticContext("test", "memory"));

        Assert.True(dispatcher.TryPublish("temperature", 1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        for (var value = 2; value <= 10_000; value++)
        {
            Assert.True(dispatcher.TryPublish("temperature", value));
        }

        release.SetResult();
        await dispatcher.DisposeAsync();

        Assert.Equal(new[] { 1, 10_000 }, delivered);
        Assert.Equal(9_998, Volatile.Read(ref coalesced));
    }
}
