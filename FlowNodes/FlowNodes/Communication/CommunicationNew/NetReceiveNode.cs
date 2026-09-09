using CommunicationNew.Core;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>等待 TCP、UDP 或串口设备的下一次被动接收事件。</summary>
[FlowTool("新通讯接收", "InboxArrowDown", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 52, Description = "等待下一个 TCP 数据块、UDP 数据报或串口数据块")]
internal sealed class NetReceiveNode : NetDeviceNodeBase
{
    public NetReceiveNode(CommunicationManager manager) : base(manager) { }

    private ENetDataFormat _format = ENetDataFormat.UTF8;
    [NodeProperty(DisplayName = "数据格式", Group = "1.基础", Order = 1)]
    public ENetDataFormat Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    private int _receiveTimeoutMs = 3000;
    [NodeProperty(DisplayName = "接收超时(ms)", Group = "1.基础", Order = 2, Min = 0, Description = "0=无限等待，可由停止按钮或节点执行超时中断")]
    public int ReceiveTimeoutMs
    {
        get => _receiveTimeoutMs;
        set => SetProperty(ref _receiveTimeoutMs, Math.Clamp(value, 0, 3_600_000));
    }

    private string _receivedText = string.Empty;
    [NodeProperty(DisplayName = "接收文本", Group = "2.结果", Order = 10, ReadOnly = 1)]
    public string ReceivedText
    {
        get => _receivedText;
        set => SetProperty(ref _receivedText, value);
    }

    private int _receivedCount;
    [NodeProperty(DisplayName = "接收字节数", Group = "2.结果", Order = 11, ReadOnly = 1)]
    public int ReceivedCount
    {
        get => _receivedCount;
        set => SetProperty(ref _receivedCount, value);
    }

    private string _remoteAddress = string.Empty;
    [NodeProperty(DisplayName = "来源地址", Group = "2.结果", Order = 12, ReadOnly = 1)]
    public string RemoteAddress
    {
        get => _remoteAddress;
        set => SetProperty(ref _remoteAddress, value);
    }

    private DateTime _receivedAt;
    [NodeProperty(DisplayName = "接收时间", Group = "2.结果", Order = 13, ReadOnly = 1)]
    public DateTime ReceivedAt
    {
        get => _receivedAt;
        set => SetProperty(ref _receivedAt, value);
    }

    private byte[] _receivedBytes = [];
    [NodeProperty(DisplayName = "接收字节", Group = "2.结果", Order = 14, IsDisplay = false)]
    public byte[] ReceivedBytes
    {
        get => _receivedBytes;
        set => SetProperty(ref _receivedBytes, value);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ResetOutputs();

        var device = GetDevice();
        if (!NetDeviceHelper.SupportsPassiveReceive(device))
            throw await CreateFailureAsync("接收", null, detail: $"{device.ProtocolType} 不支持被动报文接收，请使用地址读写节点");

        var inbox = NetReceiveInbox.Attach(device);
        using var timeoutCts = ReceiveTimeoutMs > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : null;
        timeoutCts?.CancelAfter(ReceiveTimeoutMs);
        var receiveToken = timeoutCts?.Token ?? ct;
        try
        {
            var packet = await inbox.ReadAsync(receiveToken);

            ReceivedBytes = packet.Data;
            ReceivedCount = packet.Data.Length;
            ReceivedText = NetDeviceHelper.FormatData(packet.Data, Format);
            RemoteAddress = packet.RemoteAddress ?? string.Empty;
            ReceivedAt = packet.Timestamp;
            await NodeLog.InfoAsync($"接收 [{DeviceName}]：{ReceivedCount} 字节，来源 {RemoteAddress}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex) when (timeoutCts?.IsCancellationRequested == true)
        {
            throw await CreateFailureAsync("接收", null, ex, $"{ReceiveTimeoutMs}ms 内未收到数据");
        }
    }

    private void ResetOutputs()
    {
        ReceivedBytes = [];
        ReceivedCount = 0;
        ReceivedText = string.Empty;
        RemoteAddress = string.Empty;
        ReceivedAt = default;
    }
}
