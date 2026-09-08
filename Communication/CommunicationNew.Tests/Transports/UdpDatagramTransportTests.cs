using System.Net;
using CommunicationNew.Transports;
using Xunit;

namespace CommunicationNew.Tests.Transports;

public sealed class UdpDatagramTransportTests
{
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
