using System.Net;
using System.Net.Sockets;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class ModbusTcpClientCompatibilityTests
{
    [Fact]
    public async Task Legacy_read_api_uses_the_new_connection_seam_and_preserves_register_bytes()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            await using var device = new ModbusTcpClientDevice(new ModbusTcpParam
            {
                ServerIP = IPAddress.Loopback.ToString(),
                Port = port,
                SlaveId = 1,
                TimeoutMs = 1000,
                RetryCount = 1,
            });

            var acceptTask = listener.AcceptTcpClientAsync();
            Assert.True(await device.ConnectAsync());
            using var peer = await acceptTask;
            await using var stream = peer.GetStream();

            Assert.IsAssignableFrom<IConnection>(device);
            Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);

            var readTask = device.ReadAsync("40001", 2);
            var request = new byte[12];
            await stream.ReadExactlyAsync(request);
            Assert.Equal(new byte[] { 1, 3, 0, 0, 0, 2 }, request[6..]);

            await stream.WriteAsync(new byte[]
            {
                request[0], request[1], 0, 0, 0, 7, 1,
                3, 4, 0, 0x2A, 0, 0x2B,
            });

            Assert.Equal(new byte[] { 0, 0x2A, 0, 0x2B }, await readTask);

            await device.DisconnectAsync();
            Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
        }
        finally
        {
            listener.Stop();
        }
    }
}
