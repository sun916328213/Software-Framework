// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusTcpClientCompatibilityTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、System.Net.Sockets、Communication.Core.Abstractions、Communication.Core.Interfaces、Communication.Core.Models、Communication.Core.Services。导入不代表所有类型都会在运行时加载。
using System.Net;
using System.Net.Sockets;
using Communication.Core.Abstractions;
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Communication.Core.Services;
using Communication.Runtime.Core.Services;
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// ModbusTcpClientCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusTcpClientCompatibilityTests
{
    /// <summary>
    /// 验证 Auto_reader_combines_adjacent_points_into_one_modbus_request 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、ToString、AddRange、AcceptTcpClientAsync、True；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Legacy_read_api_uses_the_new_connection_seam_and_preserves_register_bytes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、ToString、AcceptTcpClientAsync、True、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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
