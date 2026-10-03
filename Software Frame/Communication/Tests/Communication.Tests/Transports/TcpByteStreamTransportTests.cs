// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TcpByteStreamTransportTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、System.Net、System.Net.Sockets、Communication.Runtime.Transports、Xunit。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Communication.Runtime.Transports;
using Xunit;

namespace Communication.Tests.Transports;

/// <summary>
/// TcpByteStreamTransportTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class TcpByteStreamTransportTests
{
    /// <summary>
    /// 验证 Repeated_open_is_idempotent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、ToString、AcceptTcpClientAsync、OpenAsync、True；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Repeated_open_is_idempotent()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var transport = new TcpByteStreamTransport(
                IPAddress.Loopback.ToString(),
                endpoint.Port);
            var acceptTask = listener.AcceptTcpClientAsync();

            await transport.OpenAsync();
            using var peer = await acceptTask;
            await transport.OpenAsync();

            Assert.True(transport.IsOpen);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// 验证 Open_transport_exchanges_bytes_with_a_loopback_peer 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、ToString、AcceptTcpClientAsync、OpenAsync、GetStream；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Open_transport_exchanges_bytes_with_a_loopback_peer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var transport = new TcpByteStreamTransport(
                IPAddress.Loopback.ToString(),
                endpoint.Port);

            var acceptTask = listener.AcceptTcpClientAsync();
            await transport.OpenAsync();
            using var peer = await acceptTask;
            await using var peerStream = peer.GetStream();

            await transport.Output.WriteAsync(new byte[] { 0x11, 0x22 });
            var sent = new byte[2];
            await peerStream.ReadExactlyAsync(sent);
            Assert.Equal(new byte[] { 0x11, 0x22 }, sent);

            await peerStream.WriteAsync(new byte[] { 0x33, 0x44 });
            var received = await transport.Input.ReadAsync();
            Assert.Equal(new byte[] { 0x33, 0x44 }, received.Buffer.Slice(0, 2).ToArray());
            transport.Input.AdvanceTo(received.Buffer.GetPosition(2));

            await transport.CloseAsync();
            Assert.False(transport.IsOpen);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// 验证 Pending_read_observes_caller_cancellation 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、ToString、AcceptTcpClientAsync、OpenAsync、AsTask；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Pending_read_observes_caller_cancellation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var transport = new TcpByteStreamTransport(
                IPAddress.Loopback.ToString(),
                endpoint.Port);
            var acceptTask = listener.AcceptTcpClientAsync();
            await transport.OpenAsync();
            using var peer = await acceptTask;
            using var cancellation = new CancellationTokenSource();

            // 对端保持连接但不发送数据，确保读取只能由调用方取消结束。
            var readTask = transport.Input.ReadAsync(cancellation.Token).AsTask();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// 验证 Remote_close_completes_input_and_local_close_remains_idempotent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、ToString、AcceptTcpClientAsync、OpenAsync、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Remote_close_completes_input_and_local_close_remains_idempotent()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var transport = new TcpByteStreamTransport(
                IPAddress.Loopback.ToString(),
                endpoint.Port);
            var acceptTask = listener.AcceptTcpClientAsync();
            await transport.OpenAsync();
            using var peer = await acceptTask;

            peer.Dispose();
            var read = await transport.Input.ReadAsync().AsTask()
                .WaitAsync(TimeSpan.FromSeconds(1));
            transport.Input.AdvanceTo(read.Buffer.End);

            Assert.True(read.IsCompleted);
            await transport.CloseAsync();
            await transport.CloseAsync();
            Assert.False(transport.IsOpen);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// 验证 Dispose_is_idempotent_and_prevents_reopening 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToString、DisposeAsync、ThrowsAsync、AsTask、OpenAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Dispose_is_idempotent_and_prevents_reopening()
    {
        var transport = new TcpByteStreamTransport(
            IPAddress.Loopback.ToString(),
            1);

        await transport.DisposeAsync();
        await transport.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => transport.OpenAsync().AsTask());
        Assert.False(transport.IsOpen);
    }
}
