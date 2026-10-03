// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FluentModbusTcpLoopbackTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、System.Net.Sockets、Communication.Core.Abstractions、Communication.Core.Protocols.Modbus.ModbusTcp、Communication.Runtime.Protocols.Modbus.ModbusTcp、Xunit。导入不代表所有类型都会在运行时加载。
using System.Net;
using System.Net.Sockets;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace Communication.Tests.Protocols.Modbus;

/// <summary>验证 FluentModbus 生产从站 adapter 与项目 TCP 客户端的真实网络协作。</summary>
public sealed class FluentModbusTcpLoopbackTests
{
    /// <summary>
    /// 验证 Production_server_and_client_share_holding_register_data_over_tcp 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ReserveTcpPort、ToString、True、ConnectAsync、WriteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 ReserveTcpPort 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、Stop；具体条件和先后次序见方法体。
    /// </summary>
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
