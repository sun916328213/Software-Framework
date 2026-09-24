using System.Net;
using System.Net.Sockets;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Socket;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class TcpServerDeviceCompatibilityTests
{
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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
