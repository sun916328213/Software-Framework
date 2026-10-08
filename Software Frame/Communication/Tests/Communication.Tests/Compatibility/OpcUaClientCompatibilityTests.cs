// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：OpcUaClientCompatibilityTests、ScriptedOpcUaClientAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Abstractions、Communication.Core.Protocols.OpcUa、Communication.Runtime.Protocols.OpcUa、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Abstractions;
using Communication.Core.Protocols.OpcUa;
using Communication.Runtime.Protocols.OpcUa;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// OpcUaClientCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class OpcUaClientCompatibilityTests
{
    /// <summary>
    /// 测试相关入口 Client_adapter_performs_real_session_operations_through_legacy_surface；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、Equal、ReadAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Client_adapter_performs_real_session_operations_through_legacy_surface()
    {
        var adapter = new ScriptedOpcUaClientAdapter
        {
            ReadResult = new byte[] { 0x2A },
        };
        await using var device = new OpcUaClientDevice(new OpcUaParam
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
        }, adapter);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        Assert.Equal(new byte[] { 0x2A }, await device.ReadAsync("ns=2;s=Value", 1));
        Assert.True(await device.WriteAsync("ns=2;s=Value", new byte[] { 0x2B }));
        Assert.Equal(("ns=2;s=Value", new byte[] { 0x2B }), adapter.LastWrite);

        await device.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
        Assert.Equal(1, adapter.DisconnectCount);
    }

    /// <summary>
    /// ScriptedOpcUaClientAdapter 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IOpcUaClientAdapter。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedOpcUaClientAdapter : IOpcUaClientAdapter
    {
        public bool IsConnected { get; private set; }
        public int DisconnectCount { get; private set; }
        public byte[] ReadResult { get; init; } = Array.Empty<byte>();
        public (string NodeId, byte[] Value)? LastWrite { get; private set; }

        /// <summary>
        /// 测试相关入口 ConnectAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ValueTask ConnectAsync(CancellationToken cancellationToken)
        {
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 测试相关入口 DisconnectAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ValueTask DisconnectAsync(CancellationToken cancellationToken)
        {
            if (IsConnected)
            {
                IsConnected = false;
                DisconnectCount++;
            }
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 测试相关入口 ReadAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<byte[]> ReadAsync(string nodeId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ReadResult);

        /// <summary>
        /// 测试相关入口 WriteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToArray；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask WriteAsync(
            string nodeId,
            byte[] value,
            CancellationToken cancellationToken)
        {
            LastWrite = (nodeId, value.ToArray());
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 测试相关入口 DisposeAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 DisconnectAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask DisposeAsync() => await DisconnectAsync(CancellationToken.None);
    }
}
