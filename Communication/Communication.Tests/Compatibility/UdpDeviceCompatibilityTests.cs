using System.Net;
using System.Net.Sockets;
using CommunicationNew.Abstractions;
using CommunicationNew.Core.EventArgs;
using CommunicationNew.Protocols.Socket;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class UdpDeviceCompatibilityTests
{
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

    private static int ReserveUdpPort()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }
}
