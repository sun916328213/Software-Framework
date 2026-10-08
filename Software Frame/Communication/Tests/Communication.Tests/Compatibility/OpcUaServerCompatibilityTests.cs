// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：OpcUaServerCompatibilityTests、InMemoryOpcUaServerAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Abstractions、Communication.Core.Protocols.OpcUa、Communication.Runtime.Protocols.OpcUa、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Abstractions;
using Communication.Core.Protocols.OpcUa;
using Communication.Runtime.Protocols.OpcUa;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// OpcUaServerCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class OpcUaServerCompatibilityTests
{
    /// <summary>
    /// 测试相关入口 Server_adapter_exposes_registered_nodes_and_owned_lifecycle；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterNode、IsAssignableFrom、True、ConnectAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Server_adapter_exposes_registered_nodes_and_owned_lifecycle()
    {
        var adapter = new InMemoryOpcUaServerAdapter();
        await using var device = new OpcUaServerDevice(new OpcUaParam
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840/CommunicationNew",
        }, adapter);
        device.RegisterNode("ns=2;s=Value", new byte[] { 0x2A });

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        Assert.Equal(new byte[] { 0x2A }, await device.ReadAsync("ns=2;s=Value", 1));

        Assert.True(await device.WriteAsync("ns=2;s=Value", new byte[] { 0x2B }));
        Assert.Equal(new byte[] { 0x2B }, await device.ReadAsync("ns=2;s=Value", 1));

        await device.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
        Assert.Equal(1, adapter.StopCount);
    }

    /// <summary>
    /// InMemoryOpcUaServerAdapter 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IOpcUaServerAdapter。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class InMemoryOpcUaServerAdapter : IOpcUaServerAdapter
    {
        private readonly Dictionary<string, byte[]> _values = new();

        public bool IsRunning { get; private set; }
        public int StopCount { get; private set; }
        public IReadOnlyCollection<string> Nodes => _values.Keys.ToArray();

        /// <summary>
        /// 测试相关入口 RegisterNode；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToArray；具体条件和先后次序见方法体。
        /// </summary>
        public void RegisterNode(string nodeId, byte[] initialValue) =>
            _values[nodeId] = initialValue.ToArray();

        /// <summary>
        /// 测试相关入口 StartAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ValueTask StartAsync(CancellationToken cancellationToken)
        {
            IsRunning = true;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 测试相关入口 StopAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ValueTask StopAsync(CancellationToken cancellationToken)
        {
            if (IsRunning)
            {
                IsRunning = false;
                StopCount++;
            }
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 测试相关入口 Read；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToArray；具体条件和先后次序见方法体。
        /// </summary>
        public byte[] Read(string nodeId) => _values[nodeId].ToArray();

        /// <summary>
        /// 测试相关入口 Write；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToArray；具体条件和先后次序见方法体。
        /// </summary>
        public void Write(string nodeId, byte[] value) => _values[nodeId] = value.ToArray();

        /// <summary>
        /// 测试相关入口 DisposeAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 StopAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
    }
}
