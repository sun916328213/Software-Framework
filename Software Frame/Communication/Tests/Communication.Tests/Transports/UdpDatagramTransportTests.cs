// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：UdpDatagramTransportTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、Communication.Runtime.Transports、Xunit。导入不代表所有类型都会在运行时加载。
using System.Net;
using Communication.Runtime.Transports;
using Xunit;

namespace Communication.Tests.Transports;

/// <summary>
/// UdpDatagramTransportTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class UdpDatagramTransportTests
{
    /// <summary>
    /// 验证 Repeated_open_is_idempotent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 OpenAsync、True、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Repeated_open_is_idempotent()
    {
        await using var transport = new UdpDatagramTransport(
            new IPEndPoint(IPAddress.Loopback, 0));

        await transport.OpenAsync();
        var firstEndpoint = transport.LocalEndpoint;
        await transport.OpenAsync();

        Assert.True(transport.IsOpen);
        Assert.Equal(firstEndpoint, transport.LocalEndpoint);
    }

    /// <summary>
    /// 验证 Open_transports_exchange_one_complete_datagram_and_close_idempotently 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 OpenAsync、SendAsync、ReceiveAsync、Equal、ToArray；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Open_transports_exchange_one_complete_datagram_and_close_idempotently()
    {
        await using var receiver = new UdpDatagramTransport(
            new IPEndPoint(IPAddress.Loopback, 0));
        await using var sender = new UdpDatagramTransport(
            new IPEndPoint(IPAddress.Loopback, 0));

        await receiver.OpenAsync();
        await sender.OpenAsync();

        await sender.SendAsync(
            new Datagram(new byte[] { 0x10, 0x20, 0x30 }, receiver.LocalEndpoint!));
        var received = await receiver.ReceiveAsync();

        Assert.Equal(new byte[] { 0x10, 0x20, 0x30 }, received.Payload.ToArray());
        Assert.Equal(sender.LocalEndpoint, received.RemoteEndpoint);

        await receiver.CloseAsync();
        await receiver.CloseAsync();
        Assert.False(receiver.IsOpen);
    }

    /// <summary>
    /// 验证 Pending_receive_observes_caller_cancellation 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 OpenAsync、AsTask、ReceiveAsync、Cancel、ThrowsAnyAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Pending_receive_observes_caller_cancellation()
    {
        await using var transport = new UdpDatagramTransport(
            new IPEndPoint(IPAddress.Loopback, 0));
        await transport.OpenAsync();
        using var cancellation = new CancellationTokenSource();

        var receiveTask = transport.ReceiveAsync(cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receiveTask);
    }

    /// <summary>
    /// 验证 Dispose_is_idempotent_and_prevents_reopening 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 DisposeAsync、ThrowsAsync、AsTask、OpenAsync、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Dispose_is_idempotent_and_prevents_reopening()
    {
        var transport = new UdpDatagramTransport(
            new IPEndPoint(IPAddress.Loopback, 0));

        await transport.DisposeAsync();
        await transport.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => transport.OpenAsync().AsTask());
        Assert.False(transport.IsOpen);
    }
}
