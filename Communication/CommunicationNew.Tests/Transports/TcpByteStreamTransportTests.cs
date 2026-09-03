using System.Buffers;
using System.Net;
using System.Net.Sockets;
using CommunicationNew.Transports;
using Xunit;

namespace CommunicationNew.Tests.Transports;

public sealed class TcpByteStreamTransportTests
{
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
}
