// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetReceiveInbox、DeviceInbox、ReceivedPacket。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、System.Runtime.CompilerServices、System.Threading.Channels、Communication.Core、Communication.Runtime.Core、Communication.Core.EventArgs。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.EventArgs;

namespace FlowNodes.Communication;

/// <summary>
/// 为每个原始通讯设备保留一个有界收件箱，避免流程节点尚未执行时丢失已到达数据。
/// 多个接收节点竞争消费数据，每个数据块只会交给一个节点。
/// </summary>
internal static class NetReceiveInbox
{
    private const int Capacity = 256;
    private static readonly ConditionalWeakTable<ICommunicationDevice, DeviceInbox> Inboxes = new();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Attach。
    /// 正文中的关键协作调用包括 ThrowIfNull、GetValue；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    public static DeviceInbox Attach(ICommunicationDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        // ConditionalWeakTable 的 valueFactory 在竞争时可能被调用多次；
        // 这里必须串行化，否则被丢弃的 Inbox 仍会留下事件订阅。
        lock (Inboxes)
        {
            return Inboxes.GetValue(device, static value => new DeviceInbox(value));
        }
    }

    /// <summary>
    /// DeviceInbox 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
    /// </summary>
    internal sealed class DeviceInbox
    {
        private readonly Channel<ReceivedPacket> _channel = Channel.CreateBounded<ReceivedPacket>(
            new BoundedChannelOptions(Capacity)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
                AllowSynchronousContinuations = false,
            });

        /// <summary>
        /// 初始化 DeviceInbox 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DeviceInbox(ICommunicationDevice device)
        {
            device.DataReceived += OnDataReceived;
        }

        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadAsync。
        /// 正文中的关键协作调用包括 ReadAsync；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<ReceivedPacket> ReadAsync(CancellationToken ct) =>
            _channel.Reader.ReadAsync(ct);

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnDataReceived。
        /// 正文中的关键协作调用包括 TryWrite、ToArray；具体条件和先后次序见方法体。
        /// </summary>
        private void OnDataReceived(object? sender, DataReceivedEventArgs args)
        {
            if (args.Data is not { Length: > 0 }) return;
            _channel.Writer.TryWrite(new ReceivedPacket(
                args.Data.ToArray(),
                args.RemoteAddress,
                args.Timestamp));
        }
    }

    /// <summary>
    /// ReceivedPacket 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
    /// </summary>
    internal sealed record ReceivedPacket(byte[] Data, string? RemoteAddress, DateTime Timestamp);
}
