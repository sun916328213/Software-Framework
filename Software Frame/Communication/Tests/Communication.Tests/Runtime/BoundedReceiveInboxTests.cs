// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：BoundedReceiveInboxTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Diagnostics.Metrics、System.Threading.Channels、Communication.Core.Abstractions、Communication.Runtime、Xunit。导入不代表所有类型都会在运行时加载。
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Communication.Core.Abstractions;
using Communication.Runtime;
using Xunit;

namespace Communication.Tests.Runtime;

/// <summary>
/// BoundedReceiveInboxTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class BoundedReceiveInboxTests
{
    /// <summary>
    /// 验证 Packet_received_before_consumer_starts_is_preserved 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BeginGeneration、Publish、ReceiveAsync、Equal、ToArray；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Packet_received_before_consumer_starts_is_preserved()
    {
        using var inbox = new BoundedReceiveInbox(2, ReceiveOverflowPolicy.Reject);
        var context = new CommunicationDiagnosticContext("receive-test", "memory");
        inbox.BeginGeneration();

        inbox.Publish(new ReceivedPacket([0x01], "peer"), context);

        var packet = await inbox.ReceiveAsync();
        Assert.Equal(new byte[] { 0x01 }, packet.Data.ToArray());
        Assert.Equal("peer", packet.RemoteAddress);
    }

    /// <summary>
    /// 验证 Multiple_consumers_do_not_receive_the_same_packet 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BeginGeneration、AsTask、ReceiveAsync、Publish、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Multiple_consumers_do_not_receive_the_same_packet()
    {
        using var inbox = new BoundedReceiveInbox(2, ReceiveOverflowPolicy.Reject);
        var context = new CommunicationDiagnosticContext("receive-test", "memory");
        inbox.BeginGeneration();
        var firstConsumer = inbox.ReceiveAsync().AsTask();
        var secondConsumer = inbox.ReceiveAsync().AsTask();

        inbox.Publish(new ReceivedPacket([0x01]), context);
        inbox.Publish(new ReceivedPacket([0x02]), context);

        var values = new[]
        {
            (await firstConsumer).Data.Span[0],
            (await secondConsumer).Data.Span[0],
        };
        Assert.Equal(new byte[] { 0x01, 0x02 }, values.OrderBy(value => value));
    }

    /// <summary>
    /// 验证 Ending_generation_releases_pending_consumer 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BeginGeneration、AsTask、ReceiveAsync、EndGeneration、ThrowsAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Ending_generation_releases_pending_consumer()
    {
        using var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        var generation = inbox.BeginGeneration();
        var pending = inbox.ReceiveAsync().AsTask();

        inbox.EndGeneration(generation);

        await Assert.ThrowsAsync<ChannelClosedException>(() => pending);
    }

    /// <summary>
    /// 验证 Caller_cancellation_releases_pending_consumer 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BeginGeneration、AsTask、ReceiveAsync、Cancel、ThrowsAnyAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Caller_cancellation_releases_pending_consumer()
    {
        using var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        inbox.BeginGeneration();
        using var cancellation = new CancellationTokenSource();
        var pending = inbox.ReceiveAsync(cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    /// <summary>
    /// 验证 Dispose_releases_pending_consumer 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BeginGeneration、AsTask、ReceiveAsync、Dispose、ThrowsAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Dispose_releases_pending_consumer()
    {
        var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        inbox.BeginGeneration();
        var pending = inbox.ReceiveAsync().AsTask();

        inbox.Dispose();

        await Assert.ThrowsAsync<ChannelClosedException>(() => pending);
    }

    /// <summary>
    /// 验证 Late_old_generation_end_cannot_close_reconnected_inbox 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BeginGeneration、EndGeneration、Publish、Equal、ReceiveAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Late_old_generation_end_cannot_close_reconnected_inbox()
    {
        using var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        var context = new CommunicationDiagnosticContext("receive-test", "memory");
        var oldGeneration = inbox.BeginGeneration();
        var currentGeneration = inbox.BeginGeneration();

        inbox.EndGeneration(oldGeneration);
        inbox.Publish(new ReceivedPacket([0x2A]), context);

        Assert.Equal(0x2A, (await inbox.ReceiveAsync()).Data.Span[0]);
        inbox.EndGeneration(currentGeneration);
    }

    /// <summary>
    /// 验证 Reliable_stream_rejects_full_inbox_instead_of_dropping_bytes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BeginGeneration、Publish、Throws、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Reliable_stream_rejects_full_inbox_instead_of_dropping_bytes()
    {
        using var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        var context = new CommunicationDiagnosticContext("receive-test", "memory");
        inbox.BeginGeneration();
        inbox.Publish(new ReceivedPacket([0x01]), context);

        var exception = Assert.Throws<ReceiveQueueFullException>(
            () => inbox.Publish(new ReceivedPacket([0x02]), context));

        Assert.Equal(1, exception.Capacity);
    }

    /// <summary>
    /// 验证 Datagram_inbox_keeps_latest_packets_and_reports_drop 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewGuid、EnableMeasurementEvents、SetMeasurementEventCallback、Equals、Add；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Datagram_inbox_keeps_latest_packets_and_reports_drop()
    {
        var protocolTag = $"receive-{Guid.NewGuid():N}";
        var context = new CommunicationDiagnosticContext(protocolTag, "udp");
        long dropped = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.receive.dropped")
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
                    Interlocked.Add(ref dropped, value);
                    break;
                }
            }
        });
        listener.Start();

        using var inbox = new BoundedReceiveInbox(2, ReceiveOverflowPolicy.DropOldest);
        inbox.BeginGeneration();
        inbox.Publish(new ReceivedPacket([0x01]), context);
        inbox.Publish(new ReceivedPacket([0x02]), context);
        inbox.Publish(new ReceivedPacket([0x03]), context);

        Assert.Equal(0x02, (await inbox.ReceiveAsync()).Data.Span[0]);
        Assert.Equal(0x03, (await inbox.ReceiveAsync()).Data.Span[0]);
        Assert.Equal(1, Volatile.Read(ref dropped));
    }
}
