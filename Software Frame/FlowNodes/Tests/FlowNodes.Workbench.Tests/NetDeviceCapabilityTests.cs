// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NetDeviceCapabilityTests、ProbeDevice、DeclaredProtocolOnlyDevice、AddressCapableProbeDevice、AddressReaderOnlyProbeDevice、AddressWriterOnlyProbeDevice、RawSenderOnlyProbeDevice、RequestReplyServerProbeDevice。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Abstractions、Communication.Core.EventArgs、Communication.Runtime.Composition。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Abstractions;
using Communication.Core.EventArgs;
using Communication.Runtime.Composition;
using FlowNodes.Communication;
using Xunit;

namespace FlowNodes.Workbench.Tests;

/// <summary>
/// T14：锁定 FlowNodes 的能力判定只看设备**真实实现的能力接口**，不看 <c>ProtocolType</c> 声明。
/// 改造前四个判定都是 <c>device.ProtocolType is ...</c>，而枚举把客户端与服务端压进同一个值
/// （Http / WebApi 同时覆盖 client 与 server），于是 HttpServerDevice / WebApiServerDevice
/// 被误判成「支持按地址读写」——节点因此绕开自己的能力提示，改用基类 ReadAsync / WriteAsync
/// 抛出的 NotSupportedException 当失败原因报出去。
/// </summary>
public sealed class NetDeviceCapabilityTests
{
    /// <summary>
    /// 全部 16 个内置设备的构造输入，顺序即 factory 产出顺序。
    /// 同时充当「设备类型名 oracle」：先钉住 factory 真的产出了这 16 个类型，后面的能力集合断言才可信。
    /// </summary>
    private static readonly (ProtocolType Protocol, DeviceRole Role, string Name)[] BuiltInDevices =
    [
        (ProtocolType.Tcp, DeviceRole.Client, "TcpClientDevice"),
        (ProtocolType.Tcp, DeviceRole.Server, "TcpServerDevice"),
        (ProtocolType.Udp, DeviceRole.Client, "UdpClientDevice"),
        (ProtocolType.Udp, DeviceRole.Server, "UdpServerDevice"),
        (ProtocolType.Serial, DeviceRole.Client, "SerialPortDevice"),
        (ProtocolType.ModbusTcp, DeviceRole.Client, "ModbusTcpClientDevice"),
        (ProtocolType.ModbusTcp, DeviceRole.Server, "ModbusTcpServerDevice"),
        (ProtocolType.ModbusRtu, DeviceRole.Client, "ModbusRtuClientDevice"),
        (ProtocolType.ModbusRtu, DeviceRole.Server, "ModbusRtuServerDevice"),
        (ProtocolType.MitsubishiMc, DeviceRole.Client, "McClientDevice"),
        (ProtocolType.OpcUa, DeviceRole.Client, "OpcUaClientDevice"),
        (ProtocolType.OpcUa, DeviceRole.Server, "OpcUaServerDevice"),
        (ProtocolType.Http, DeviceRole.Client, "HttpClientDevice"),
        (ProtocolType.Http, DeviceRole.Server, "HttpServerDevice"),
        (ProtocolType.WebApi, DeviceRole.Client, "WebApiClientDevice"),
        (ProtocolType.WebApi, DeviceRole.Server, "WebApiServerDevice"),
    ];

    // ── 1. 探针设备：判定读的是接口还是枚举，只有这里能分辨 ────────────────
    // 真实设备里「声明的协议」与「实现的接口」结论恰好重合，所以必须靠自造设备把两者拆开。

    /// <summary>只声明了协议、没有实现任何能力接口的设备：四个判定都必须为 false。</summary>
    [Theory]
    [InlineData(ProtocolType.ModbusTcp)]
    [InlineData(ProtocolType.ModbusRtu)]
    [InlineData(ProtocolType.MitsubishiMc)]
    [InlineData(ProtocolType.OpcUa)]
    [InlineData(ProtocolType.Http)]
    [InlineData(ProtocolType.WebApi)]
    public void 地址能力取决于已实现的接口而不是声明的协议(ProtocolType declared)
    {
        var device = new DeclaredProtocolOnlyDevice(declared);

        Assert.False(NetDeviceHelper.SupportsAddressAccess(device));
    }

    /// <summary>同一个探针：声明成 Tcp / Udp / Serial 也换不来原始收发能力。</summary>
    [Theory]
    [InlineData(ProtocolType.Tcp)]
    [InlineData(ProtocolType.Udp)]
    [InlineData(ProtocolType.Serial)]
    public void 原始收发能力取决于已实现的接口而不是声明的协议(ProtocolType declared)
    {
        var device = new DeclaredProtocolOnlyDevice(declared);

        Assert.False(NetDeviceHelper.SupportsRawSend(device));
        Assert.False(NetDeviceHelper.SupportsPassiveReceive(device));
        Assert.False(NetDeviceHelper.SupportsRequestReply(device));
    }

    /// <summary>
    /// 地址能力要求读写**同时**具备：只实现一半的设备不能被判为「支持按地址访问」，
    /// 否则 NetReadNode / NetWriteNode 的守卫会放行一个它其实做不了的操作。
    /// </summary>
    [Fact]
    public void 地址能力要求读写能力同时具备()
    {
        Assert.True(NetDeviceHelper.SupportsAddressAccess(new AddressCapableProbeDevice(ProtocolType.Tcp)));
        Assert.False(NetDeviceHelper.SupportsAddressAccess(new AddressReaderOnlyProbeDevice(ProtocolType.ModbusTcp)));
        Assert.False(NetDeviceHelper.SupportsAddressAccess(new AddressWriterOnlyProbeDevice(ProtocolType.ModbusTcp)));
    }

    /// <summary>
    /// 反向对照：实现了能力接口的探针必须被放行，且地址能力与原始收发能力互不串门。
    /// 防止「一律返回 false」这种把上面几组用例刷绿的假修复。
    /// </summary>
    [Fact]
    public void 实现能力接口后判定必须放行且不串门()
    {
        var device = new AddressCapableProbeDevice(ProtocolType.Tcp);

        Assert.True(NetDeviceHelper.SupportsAddressAccess(device));
        Assert.False(NetDeviceHelper.SupportsRawSend(device));
        Assert.False(NetDeviceHelper.SupportsPassiveReceive(device));
        Assert.False(NetDeviceHelper.SupportsRequestReply(device));
    }

    /// <summary>
    /// 请求-回复判定只能看 <c>IRequestReplyClient</c>，不能看「角色 + 原始发送」。
    /// </summary>
    /// <remarks>
    /// 这条用例是**必需的**，不是补刀：16 个内置设备里恰好没有能区分这两种写法的样本 ——
    /// 三个实现 <c>IRawSender</c> 的客户端（Tcp / Udp / Serial）**同时**实现了 <c>IRequestReplyClient</c>，
    /// 而两个实现了 <c>IRawSender</c> 的服务端角色是 <c>Server</c>，被 <c>Role == Client</c> 这一半挡住，
    /// 于是 <c>Role == Client &amp;&amp; device is IRawSender</c> 与 <c>device is IRequestReplyClient</c>
    /// 在这 16 个设备上**逐字等价**。换句话说，改造前那行「看起来对」只是被角色恰好补偿了，
    /// 而不是判据正确 —— 一个只发原始报文、不支持请求-回复的客户端会被它误判为支持。
    /// 只有自造探针才能把这两种写法分开，因此这里必须各给一个反例。
    /// </remarks>
    [Fact]
    public void 请求回复判定只看能力接口而不看角色()
    {
        // 客户端角色 + 原始发送能力，但**没有**请求-回复能力：必须判为不支持。
        var rawOnly = new RawSenderOnlyProbeDevice(ProtocolType.Tcp, DeviceRole.Client);
        Assert.True(NetDeviceHelper.SupportsRawSend(rawOnly));
        Assert.False(NetDeviceHelper.SupportsRequestReply(rawOnly));

        // 服务端角色但**实现**了请求-回复能力：必须判为支持（角色不是判据）。
        var serverRequestReply = new RequestReplyServerProbeDevice(ProtocolType.Tcp, DeviceRole.Server);
        Assert.True(NetDeviceHelper.SupportsRequestReply(serverRequestReply));
    }

    // ── 2. 16 个真实设备 ────────────────────────────────────────────────

    /// <summary>先钉住 factory 产出：16 组协议/角色必须逐一映射到预期的设备类型。</summary>
    [Fact]
    public void 内置设备工厂产出预期的十六个设备类型()
    {
        var devices = CreateBuiltInDevices();
        try
        {
            Assert.Equal(
                BuiltInDevices.Select(entry => entry.Name).ToArray(),
                devices.Select(device => device.GetType().Name).ToArray());
        }
        finally
        {
            DisposeAll(devices);
        }
    }

    /// <summary>每个内置设备的四个判定都必须与它实现的能力接口逐项一致。</summary>
    [Fact]
    public void 能力判定与每个内置设备的能力接口逐项一致()
    {
        var devices = CreateBuiltInDevices();
        try
        {
            foreach (var device in devices)
            {
                Assert.Equal(
                    device is IAddressReader && device is IAddressWriter,
                    NetDeviceHelper.SupportsAddressAccess(device));
                Assert.Equal(device is IRawSender, NetDeviceHelper.SupportsRawSend(device));
                Assert.Equal(device is IRequestReplyClient, NetDeviceHelper.SupportsRequestReply(device));
                Assert.Equal(
                    device is IReceiveSource<global::Communication.Core.Abstractions.ReceivedPacket>,
                    NetDeviceHelper.SupportsPassiveReceive(device));
            }
        }
        finally
        {
            DisposeAll(devices);
        }
    }

    /// <summary>
    /// 能力集合按设备类型名逐项锁定。
    /// 地址能力这一条是本次修复的核心：HttpServerDevice / WebApiServerDevice **不在**名单里，
    /// 而它们的客户端在——退回枚举判定会让两个 server 混进来，本用例立刻变红。
    /// 被动接收这一条同时钉住泛型实参必须是 <c>Communication.Core.Abstractions.ReceivedPacket</c>：
    /// 本模块另有一个同名的 <c>NetReceiveInbox.ReceivedPacket</c>，接错了判定就恒为 false，
    /// 本用例的集合会变成空。
    /// </summary>
    [Fact]
    public void 四类能力集合按设备类型名逐项锁定()
    {
        var devices = CreateBuiltInDevices();
        try
        {
            Assert.Equal(
                [
                    "HttpClientDevice",
                    "McClientDevice",
                    "ModbusRtuClientDevice",
                    "ModbusRtuServerDevice",
                    "ModbusTcpClientDevice",
                    "ModbusTcpServerDevice",
                    "OpcUaClientDevice",
                    "OpcUaServerDevice",
                    "WebApiClientDevice",
                ],
                NamesOf(devices, NetDeviceHelper.SupportsAddressAccess));

            Assert.Equal(
                ["SerialPortDevice", "TcpClientDevice", "TcpServerDevice", "UdpClientDevice", "UdpServerDevice"],
                NamesOf(devices, NetDeviceHelper.SupportsRawSend));

            Assert.Equal(
                ["SerialPortDevice", "TcpClientDevice", "TcpServerDevice", "UdpClientDevice", "UdpServerDevice"],
                NamesOf(devices, NetDeviceHelper.SupportsPassiveReceive));

            Assert.Equal(
                ["SerialPortDevice", "TcpClientDevice", "UdpClientDevice"],
                NamesOf(devices, NetDeviceHelper.SupportsRequestReply));
        }
        finally
        {
            DisposeAll(devices);
        }
    }

    /// <summary>
    /// 缺陷本体的最小复现：HTTP / WebApi 的客户端具备地址语义，服务端不具备。
    /// 改造前四者全部被判为「支持按地址读写」，节点于是把基类的 NotSupportedException
    /// 当成失败原因，丢掉了「请改用接收/发送节点」这条能直接指导操作的提示。
    /// </summary>
    [Fact]
    public void HTTP与WebApi的服务端不具备地址能力而其客户端具备()
    {
        var devices = CreateBuiltInDevices();
        try
        {
            Assert.True(NetDeviceHelper.SupportsAddressAccess(ByName(devices, "HttpClientDevice")));
            Assert.True(NetDeviceHelper.SupportsAddressAccess(ByName(devices, "WebApiClientDevice")));
            Assert.False(NetDeviceHelper.SupportsAddressAccess(ByName(devices, "HttpServerDevice")));
            Assert.False(NetDeviceHelper.SupportsAddressAccess(ByName(devices, "WebApiServerDevice")));
        }
        finally
        {
            DisposeAll(devices);
        }
    }

    // ── 辅助 ────────────────────────────────────────────────────────────

    private static ICommunicationDevice[] CreateBuiltInDevices()
    {
        var factory = CommunicationDefaults.CreateDefaultFactory();
        return BuiltInDevices
            .Select(entry => factory.Create(CommunicationParamFactory.CreateParam(entry.Protocol, entry.Role)))
            .ToArray();
    }

    private static string[] NamesOf(
        ICommunicationDevice[] devices,
        Func<ICommunicationDevice, bool> predicate) =>
        devices.Where(predicate)
            .Select(device => device.GetType().Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static ICommunicationDevice ByName(ICommunicationDevice[] devices, string typeName) =>
        devices.Single(device => device.GetType().Name == typeName);

    private static void DisposeAll(ICommunicationDevice[] devices)
    {
        foreach (var device in devices)
        {
            device.Dispose();
        }
    }

    // ── 探针设备 ────────────────────────────────────────────────────────

    /// <summary>
    /// 探针基类：只声明协议，不实现任何能力接口。
    /// 事件用显式空访问器，避免「事件从未被使用」的噪声警告；
    /// 能力成员默认抛 NotSupportedException，只有真的声明了对应接口的子类才覆写它们。
    /// </summary>
    private abstract class ProbeDevice : ICommunicationDevice
    {
        protected ProbeDevice(ProtocolType protocol, DeviceRole role = DeviceRole.Client)
        {
            ProtocolType = protocol;
            Role = role;
        }

        public string DeviceName { get; set; } = "probe";

        public string DeviceId { get; } = Guid.NewGuid().ToString("N");

        public ProtocolType ProtocolType { get; }

        public DeviceRole Role { get; }

        public bool IsConnected => false;

        public int TimeoutMs { get; set; } = 1000;

        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<DataReceivedEventArgs>? DataReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred
        {
            add { }
            remove { }
        }

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public virtual Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public virtual Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> SendAsync(byte[] data, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>只声明协议：任何能力都不该被判定为具备。</summary>
    private sealed class DeclaredProtocolOnlyDevice(ProtocolType protocol) : ProbeDevice(protocol);

    /// <summary>读写能力都实现：地址判定放行，原始收发仍不放行。</summary>
    private sealed class AddressCapableProbeDevice(ProtocolType protocol)
        : ProbeDevice(protocol), IAddressReader, IAddressWriter
    {
        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    /// <summary>只实现读取：写入能力缺失，地址判定必须为 false。</summary>
    private sealed class AddressReaderOnlyProbeDevice(ProtocolType protocol)
        : ProbeDevice(protocol), IAddressReader
    {
        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
    }

    /// <summary>只实现写入：读取能力缺失，地址判定必须为 false。</summary>
    private sealed class AddressWriterOnlyProbeDevice(ProtocolType protocol)
        : ProbeDevice(protocol), IAddressWriter
    {
        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    /// <summary>
    /// 客户端角色 + 原始发送能力，但**不实现** <c>IRequestReplyClient</c>：
    /// 用来把「角色 + 原始发送」与「请求-回复能力接口」这两种写法分开。
    /// 继承来的 <c>SendAsync</c> 已满足 <see cref="IRawSender"/>。
    /// </summary>
    private sealed class RawSenderOnlyProbeDevice(ProtocolType protocol, DeviceRole role)
        : ProbeDevice(protocol, role), IRawSender;

    /// <summary>
    /// 服务端角色但**实现**了 <c>IRequestReplyClient</c>：角色不该是判据。
    /// 继承来的 <c>SendAndReceiveAsync</c> 已满足 <see cref="IRequestReplyClient"/>。
    /// </summary>
    private sealed class RequestReplyServerProbeDevice(ProtocolType protocol, DeviceRole role)
        : ProbeDevice(protocol, role), IRequestReplyClient;
}
