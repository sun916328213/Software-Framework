// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NetReceiveInboxTests、InboxProbeDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Abstractions、Communication.Core.EventArgs、FlowNodes.Communication、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Abstractions;
using Communication.Core.EventArgs;
using FlowNodes.Communication;
using Xunit;

namespace FlowNodes.Workbench.Tests;

/// <summary>
/// T15：锁定 <see cref="NetReceiveInbox"/> 的收件箱语义。改造前这个类型没有任何用例。
/// </summary>
/// <remarks>
/// 它存在的理由只有一个：流程节点还没开始执行时，设备上已经到达的数据不能丢。
/// 于是有两条必须成立的性质 —— ① 同一设备反复 <c>Attach</c> 拿到的必须是同一个收件箱
/// （否则第二次订阅就分叉成两条互不可见的队列，节点只能看见自己那一半）；
/// ② 队列有界，满时丢最旧而不是丢最新，也不是无限增长。
/// </remarks>
public sealed class NetReceiveInboxTests
{
    /// <summary>NetReceiveInbox 声明的容量上限；用满溢用例把它钉住。</summary>
    private const int Capacity = 256;

    [Fact]
    public void 同一设备重复接入必须拿到同一个收件箱而不同设备各自独立()
    {
        using var device = new InboxProbeDevice();
        using var other = new InboxProbeDevice();

        var first = NetReceiveInbox.Attach(device);

        Assert.Same(first, NetReceiveInbox.Attach(device));
        Assert.NotSame(first, NetReceiveInbox.Attach(other));
        Assert.Throws<ArgumentNullException>(() =>
        {
            NetReceiveInbox.Attach(null!);
        });
    }

    [Fact]
    public async Task 节点尚未启动时到达的数据必须按顺序保留()
    {
        using var device = new InboxProbeDevice();
        var inbox = NetReceiveInbox.Attach(device);

        // 先到达，再消费：这正是「节点还没执行」的时间窗口。
        device.Raise([0x01], "peer:1");
        device.Raise([0x02, 0x03], "peer:1");

        var first = await inbox.ReadAsync(CancellationToken.None);
        var second = await inbox.ReadAsync(CancellationToken.None);

        Assert.Equal(new byte[] { 0x01 }, first.Data);
        Assert.Equal(new byte[] { 0x02, 0x03 }, second.Data);
        Assert.Equal("peer:1", second.RemoteAddress);
    }

    [Fact]
    public async Task 多个接收节点竞争消费时每条数据只交给一个消费者()
    {
        using var device = new InboxProbeDevice();
        var inbox = NetReceiveInbox.Attach(device);

        var firstConsumer = inbox.ReadAsync(CancellationToken.None).AsTask();
        var secondConsumer = inbox.ReadAsync(CancellationToken.None).AsTask();
        device.Raise([0x01]);
        device.Raise([0x02]);

        var values = new[] { (await firstConsumer).Data[0], (await secondConsumer).Data[0] };

        Assert.Equal(new byte[] { 0x01, 0x02 }, values.OrderBy(value => value));
    }

    [Fact]
    public async Task 空数据块不入队()
    {
        using var device = new InboxProbeDevice();
        var inbox = NetReceiveInbox.Attach(device);

        device.Raise([]);

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => inbox.ReadAsync(timeout.Token).AsTask());
    }

    [Fact]
    public async Task 入队时必须复制载荷()
    {
        using var device = new InboxProbeDevice();
        var inbox = NetReceiveInbox.Attach(device);
        var payload = new byte[] { 0x41, 0x42 };

        device.Raise(payload);
        payload[0] = 0xFF;

        var packet = await inbox.ReadAsync(CancellationToken.None);
        Assert.Equal(new byte[] { 0x41, 0x42 }, packet.Data);
    }

    [Fact]
    public async Task 远端地址与时间戳必须原样带出()
    {
        using var device = new InboxProbeDevice();
        var inbox = NetReceiveInbox.Attach(device);

        var args = device.Raise([0x01], "127.0.0.1:8080");

        var packet = await inbox.ReadAsync(CancellationToken.None);
        Assert.Equal("127.0.0.1:8080", packet.RemoteAddress);
        Assert.Equal(args!.Timestamp, packet.Timestamp);
    }

    [Fact]
    public async Task 收件箱满时必须丢最旧而不是丢最新()
    {
        using var device = new InboxProbeDevice();
        var inbox = NetReceiveInbox.Attach(device);
        var dropped = 44;
        var overflow = Capacity + dropped;

        for (var i = 0; i < overflow; i++)
        {
            device.Raise([(byte)i]);
        }

        var drained = new List<byte>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        while (true)
        {
            try
            {
                drained.Add((await inbox.ReadAsync(timeout.Token)).Data[0]);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.Equal(Capacity, drained.Count);
        // 被丢掉的是最前面的 44 条，队列里留下的是最新的 256 条。
        Assert.Equal((byte)dropped, drained[0]);
        Assert.Equal((byte)(overflow - 1), drained[^1]);
    }

    /// <summary>
    /// 只负责把 <c>DataReceived</c> 事件抬起来的最小设备：同步触发，测试不需要等调度循环。
    /// </summary>
    private sealed class InboxProbeDevice : ICommunicationDevice
    {
        public string DeviceName { get; set; } = "inbox-probe";

        public string DeviceId { get; } = Guid.NewGuid().ToString("N");

        public ProtocolType ProtocolType => ProtocolType.Tcp;

        public DeviceRole Role => DeviceRole.Client;

        public bool IsConnected => false;

        public int TimeoutMs { get; set; } = 1000;

        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<DataReceivedEventArgs>? DataReceived;

        public event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred
        {
            add { }
            remove { }
        }

        /// <summary>抬起一次数据到达事件，返回事件的参数对象供断言时间戳。</summary>
        public DataReceivedEventArgs? Raise(byte[] data, string? remoteAddress = null)
        {
            var args = new DataReceivedEventArgs(data, remoteAddress);
            DataReceived?.Invoke(this, args);
            return args;
        }

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default) =>
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
}
