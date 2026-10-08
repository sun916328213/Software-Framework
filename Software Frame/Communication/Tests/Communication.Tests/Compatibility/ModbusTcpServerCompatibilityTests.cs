// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusTcpServerCompatibilityTests、ScriptedModbusTcpServerAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Abstractions、Communication.Core.Protocols.Modbus.ModbusTcp、Communication.Runtime.Protocols.Modbus.ModbusTcp、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// ModbusTcpServerCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusTcpServerCompatibilityTests
{
    /// <summary>
    /// 测试相关入口 Server_adapter_preserves_read_write_and_owned_lifecycle；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、Equal、ReadAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Server_adapter_preserves_read_write_and_owned_lifecycle()
    {
        var adapter = new ScriptedModbusTcpServerAdapter
        {
            ReadResult = new byte[] { 0x00, 0x2A },
        };
        await using var device = new ModbusTcpServerDevice(new ModbusTcpParam
        {
            Port = 1502,
            SlaveId = 1,
        }, adapter);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
        Assert.Equal(new byte[] { 0x00, 0x2A }, await device.ReadAsync("40001", 1));

        Assert.True(await device.WriteAsync("40002", new byte[] { 0x00, 0x2B }));
        Assert.Equal(("40002", new byte[] { 0x00, 0x2B }), adapter.LastWrite);

        await device.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
        Assert.Equal(1, adapter.StopCount);
    }

    /// <summary>
    /// ScriptedModbusTcpServerAdapter 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IModbusTcpServerAdapter。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedModbusTcpServerAdapter : IModbusTcpServerAdapter
    {
        public bool IsRunning { get; private set; }
        public int StopCount { get; private set; }
        public byte[] ReadResult { get; init; } = Array.Empty<byte>();
        public (string Address, byte[] Data)? LastWrite { get; private set; }

        /// <summary>
        /// 测试相关入口 Start；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Start() => IsRunning = true;

        /// <summary>
        /// 测试相关入口 Stop；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            StopCount++;
        }

        /// <summary>
        /// 测试相关入口 Read；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public byte[] Read(string address, int length) => ReadResult;

        /// <summary>
        /// 测试相关入口 Write；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToArray；具体条件和先后次序见方法体。
        /// </summary>
        public void Write(string address, byte[] data) => LastWrite = (address, data.ToArray());

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Stop();
    }
}
