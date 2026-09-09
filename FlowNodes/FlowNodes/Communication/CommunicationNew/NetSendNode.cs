using CommunicationNew.Core;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>向 TCP、UDP 或串口设备发送原始报文，不等待回复。</summary>
[FlowTool("新通讯发送", "Send", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 50, Description = "向 TCP/UDP/串口发送原始报文")]
internal sealed class NetSendNode : NetDeviceNodeBase
{
    public NetSendNode(CommunicationManager manager) : base(manager) { }

    private string _dataText = string.Empty;
    [NodeProperty(DisplayName = "发送内容", Group = "1.基础", Order = 1, Description = "可连线输入")]
    public string DataText
    {
        get => _dataText;
        set => SetProperty(ref _dataText, value);
    }

    private ENetDataFormat _format = ENetDataFormat.UTF8;
    [NodeProperty(DisplayName = "内容格式", Group = "1.基础", Order = 2)]
    public ENetDataFormat Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    private bool _sendResult;
    [NodeProperty(DisplayName = "发送结果", Group = "2.结果", Order = 10, ReadOnly = 1)]
    public bool SendResult
    {
        get => _sendResult;
        set => SetProperty(ref _sendResult, value);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        SendResult = false;

        byte[] data;
        try
        {
            data = NetDeviceHelper.ParseData(DataText, Format);
        }
        catch (Exception ex)
        {
            throw await CreateFailureAsync("发送", null, ex, $"内容解析失败：{ex.Message}");
        }

        var device = GetDevice();
        if (!NetDeviceHelper.SupportsRawSend(device))
            throw await CreateFailureAsync("发送", null, detail: $"{device.ProtocolType} 不支持原始报文发送，请使用地址读写节点");

        SendResult = await RunDeviceOperationAsync(
            "发送",
            null,
            ct,
            currentDevice => currentDevice.SendAsync(data, ct),
            device: device);
        if (!SendResult)
            throw await CreateFailureAsync("发送", null, detail: "发送未被确认");

        await NodeLog.InfoAsync($"发送 [{DeviceName}]：{data.Length} 字节");
    }
}
