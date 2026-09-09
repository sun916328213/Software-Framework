using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CommunicationNew.Core;
using CommunicationNew.Core.EventArgs;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 为每个原始通讯设备保留一个有界收件箱，避免流程节点尚未执行时丢失已到达数据。
/// 多个接收节点竞争消费数据，每个数据块只会交给一个节点。
/// </summary>
internal static class NetReceiveInbox
{
    private const int Capacity = 256;
    private static readonly ConditionalWeakTable<ICommunicationDevice, DeviceInbox> Inboxes = new();

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

        public DeviceInbox(ICommunicationDevice device)
        {
            device.DataReceived += OnDataReceived;
        }

        public ValueTask<ReceivedPacket> ReadAsync(CancellationToken ct) =>
            _channel.Reader.ReadAsync(ct);

        private void OnDataReceived(object? sender, DataReceivedEventArgs args)
        {
            if (args.Data is not { Length: > 0 }) return;
            _channel.Writer.TryWrite(new ReceivedPacket(
                args.Data.ToArray(),
                args.RemoteAddress,
                args.Timestamp));
        }
    }

    internal sealed record ReceivedPacket(byte[] Data, string? RemoteAddress, DateTime Timestamp);
}
