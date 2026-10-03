// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：UdpDeviceCompatibilityTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、System.Net.Sockets、Communication.Core.Abstractions、Communication.Core.EventArgs、Communication.Core.Protocols.Socket、Communication.Runtime.Protocols.Socket。导入不代表所有类型都会在运行时加载。
using System.Net;
using System.Net.Sockets;
using Communication.Core.Abstractions;
using Communication.Core.EventArgs;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Socket;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// UdpDeviceCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class UdpDeviceCompatibilityTests
{
    /// <summary>
    /// 验证 Legacy_udp_devices_use_connection_seam_and_preserve_datagram_boundary 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ReserveUdpPort、ToString、TrySetResult、IsAssignableFrom、True；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Legacy_udp_devices_use_connection_seam_and_preserve_datagram_boundary()
    {
        var port = ReserveUdpPort();
        await using var server = new UdpServerDevice(new SocketParam
        {
            LocalIP = IPAddress.Loopback.ToString(),
            Port = port,
        });
        await using var client = new UdpClientDevice(new SocketParam
        {
            LocalIP = IPAddress.Loopback.ToString(),
            LocalPort = 0,
            ServerIP = IPAddress.Loopback.ToString(),
            Port = port,
        });
        var received = new TaskCompletionSource<DataReceivedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        server.DataReceived += (_, args) => received.TrySetResult(args);

        Assert.IsAssignableFrom<IConnection>(server);
        Assert.IsAssignableFrom<IConnection>(client);
        var rawServer = Assert.IsAssignableFrom<IRawSender>(server);
        var rawClient = Assert.IsAssignableFrom<IRawSender>(client);
        var receiveSource = Assert.IsAssignableFrom<IReceiveSource<ReceivedPacket>>(server);
        Assert.True(await server.ConnectAsync());
        Assert.True(await client.ConnectAsync());

        Assert.True(await rawClient.SendAsync(new byte[] { 0x01, 0x02, 0x03 }));
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, result.Data);
        var packet = await receiveSource.ReceiveAsync();
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, packet.Data.ToArray());
        Assert.Equal(result.RemoteAddress, packet.RemoteAddress);
        Assert.Equal(ConnectionState.Connected, ((IConnection)server).State);
        Assert.Equal(ConnectionState.Connected, ((IConnection)client).State);

        await client.DisconnectAsync();
        await server.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, ((IConnection)client).State);
        Assert.Equal(ConnectionState.Disconnected, ((IConnection)server).State);
    }

    /// <summary>
    /// 验证 Legacy_send_and_receive_is_correlated_with_the_receive_loop 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ReserveUdpPort、ToString、TrySetResult、IsAssignableFrom、True；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Legacy_send_and_receive_is_correlated_with_the_receive_loop()
    {
        var port = ReserveUdpPort();
        await using var server = new UdpServerDevice(new SocketParam
        {
            LocalIP = IPAddress.Loopback.ToString(),
            Port = port,
        });
        await using var client = new UdpClientDevice(new SocketParam
        {
            LocalIP = IPAddress.Loopback.ToString(),
            ServerIP = IPAddress.Loopback.ToString(),
            Port = port,
            TimeoutMs = 1000,
        });
        var requestReceived = new TaskCompletionSource<DataReceivedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        server.DataReceived += (_, args) => requestReceived.TrySetResult(args);
        var requestReply = Assert.IsAssignableFrom<IRequestReplyClient>(client);
        Assert.True(await server.ConnectAsync());
        Assert.True(await client.ConnectAsync());

        var responseTask = requestReply.SendAndReceiveAsync(new byte[] { 0x11 });
        var request = await requestReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var remote = IPEndPoint.Parse(request.RemoteAddress!);
        Assert.True(await server.SendAsync(new byte[] { 0x22 }, remote.Address.ToString(), remote.Port));

        Assert.Equal(new byte[] { 0x22 }, await responseTask);
    }

    /// <summary>
    /// 验证 ReserveUdpPort 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static int ReserveUdpPort()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }
}
