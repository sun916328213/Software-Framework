using System.Net;
using System.Net.Sockets;
using CommunicationNew.Abstractions;
using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using CommunicationNew.Core.Services;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class ModbusTcpClientCompatibilityTests
{
    [Fact]
    public async Task Auto_reader_combines_adjacent_points_into_one_modbus_request()
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
            var points = new DataPointCollection();
            var first = new DataPoint("First", DataPointType.Int16, "HR1");
            var second = new DataPoint("Second", DataPointType.Int16, "HR2");
            points.AddRange(new IDataPoint[] { first, second });
            await using var reader = new DataPointAutoReader(device, points);

            var acceptTask = listener.AcceptTcpClientAsync();
            Assert.True(await device.ConnectAsync());
            using var peer = await acceptTask;
            await using var stream = peer.GetStream();

            var pollTask = reader.PollOnceAsync();
            var request = new byte[12];
            await stream.ReadExactlyAsync(request);
            Assert.Equal(new byte[] { 1, 3, 0, 0, 0, 2 }, request[6..]);

            await stream.WriteAsync(new byte[]
            {
                request[0], request[1], 0, 0, 0, 7, 1,
                3, 4, 0x12, 0x34, 0x56, 0x78,
            });
            await pollTask;

            Assert.Equal((short)0x1234, first.Current.Value);
            Assert.Equal((short)0x5678, second.Current.Value);
        }
        finally
        {
            listener.Stop();
        }
    }

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
