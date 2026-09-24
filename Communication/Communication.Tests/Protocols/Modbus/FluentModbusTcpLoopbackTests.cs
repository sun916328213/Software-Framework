using System.Net;
using System.Net.Sockets;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Modbus;

/// <summary>验证 FluentModbus 生产从站 adapter 与项目 TCP 客户端的真实网络协作。</summary>
public sealed class FluentModbusTcpLoopbackTests
{
    [Fact]
    public async Task Production_server_and_client_share_holding_register_data_over_tcp()
    {
        var port = ReserveTcpPort();
        await using var server = new ModbusTcpServerDevice(new ModbusTcpParam
        {
            Port = port,
            SlaveId = 1,
        });
        await using var client = new ModbusTcpClientDevice(new ModbusTcpParam
        {
            ServerIP = IPAddress.Loopback.ToString(),
            Port = port,
            SlaveId = 1,
            TimeoutMs = 2000,
            RetryCount = 1,
            AutoReconnect = false,
        });

        Assert.True(await server.ConnectAsync());
        Assert.True(await server.WriteAsync("40001", [0x12, 0x34]));
        Assert.True(await server.WriteAsync("C1", [0xA5]));
        Assert.True(await client.ConnectAsync());

        Assert.Equal([0x12, 0x34], await client.ReadAsync("40001", 1));
        Assert.Equal([0xA5], await client.ReadAsync("C1", 8));

        Assert.True(await client.WriteAsync("40002", [0x56, 0x78]));
        Assert.True(await client.WriteAsync("C9", [0x3C]));
        Assert.Equal([0x56, 0x78], await server.ReadAsync("40002", 1));
        Assert.Equal([0x3C], await server.ReadAsync("C9", 8));

        await client.DisconnectAsync();
        await server.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, ((IConnection)client).State);
        Assert.Equal(ConnectionState.Disconnected, ((IConnection)server).State);
    }

    private static int ReserveTcpPort()
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
}
