// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TcpServerDeviceCompatibilityTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、System.Net.Sockets、Communication.Core.Abstractions、Communication.Core.Protocols.Socket、Communication.Runtime.Protocols.Socket、Xunit。导入不代表所有类型都会在运行时加载。
using System.Net;
using System.Net.Sockets;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Socket;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// TcpServerDeviceCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class TcpServerDeviceCompatibilityTests
{
    /// <summary>
    /// 验证 Disconnecting_one_peer_keeps_server_lifecycle_and_remaining_peer_io_active 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ReservePort、ToString、Increment、TrySetResult、True；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Disconnecting_one_peer_keeps_server_lifecycle_and_remaining_peer_io_active()
    {
        var port = ReservePort();
        await using var server = new TcpServerDevice(new SocketParam
        {
            LocalIP = IPAddress.Loopback.ToString(),
            Port = port,
            Delimiter = null,
            AutoReconnect = false,
        });
        var lifecycleDisconnectedCount = 0;
        var onePeerRemains = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        server.ConnectionStateChanged += (_, e) =>
        {
            if (!e.IsConnected)
            {
                Interlocked.Increment(ref lifecycleDisconnectedCount);
            }
        };
        server.PeerConnectionChanged += (_, e) =>
        {
            if (!e.IsConnected && e.ActivePeerCount == 1)
            {
                onePeerRemains.TrySetResult();
            }
        };

        Assert.True(await server.ConnectAsync());
        using var firstPeer = new TcpClient();
        using var remainingPeer = new TcpClient();
        await firstPeer.ConnectAsync(IPAddress.Loopback, port);
        await remainingPeer.ConnectAsync(IPAddress.Loopback, port);
        await WaitUntilAsync(() => server.ConnectedClients.Count == 2);

        firstPeer.Dispose();
        await onePeerRemains.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(server.IsConnected);
        Assert.Equal(ConnectionState.Connected, ((IConnection)server).State);
        Assert.Equal(0, Volatile.Read(ref lifecycleDisconnectedCount));

        var serverPayload = new byte[] { 0x10, 0x20, 0x30 };
        Assert.True(await ((IRawSender)server).SendAsync(serverPayload));
        var receivedByPeer = new byte[serverPayload.Length];
        await remainingPeer.GetStream().ReadExactlyAsync(receivedByPeer);
        Assert.Equal(serverPayload, receivedByPeer);

        var peerPayload = new byte[] { 0x40, 0x50, 0x60 };
        await remainingPeer.GetStream().WriteAsync(peerPayload);
        var packet = await ((IReceiveSource<ReceivedPacket>)server)
            .ReceiveAsync()
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(peerPayload, packet.Data.ToArray());

        await server.DisconnectAsync();
        Assert.Equal(1, Volatile.Read(ref lifecycleDisconnectedCount));
    }

    /// <summary>
    /// 验证 ReservePort 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、Stop；具体条件和先后次序见方法体。
    /// </summary>
    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// 验证 WaitUntilAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromSeconds、condition、Delay；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
