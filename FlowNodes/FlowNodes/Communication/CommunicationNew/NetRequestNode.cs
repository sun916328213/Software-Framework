using CommunicationNew.Core;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>向 TCP、UDP 或串口客户端发送请求，并等待一帧回复。</summary>
[FlowTool("新通讯请求并等待回复", "SwapHorizontal", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 51, Description = "向 TCP/UDP/串口客户端发送请求并等待回复")]
internal sealed class NetRequestNode : NetDeviceNodeBase
{
    public NetRequestNode(CommunicationManager manager) : base(manager) { }

    private string _requestText = string.Empty;
    [NodeProperty(DisplayName = "请求内容", Group = "1.基础", Order = 1, Description = "可连线输入")]
    public string RequestText
    {
        get => _requestText;
        set => SetProperty(ref _requestText, value);
    }

    private ENetDataFormat _format = ENetDataFormat.UTF8;
    [NodeProperty(DisplayName = "数据格式", Group = "1.基础", Order = 2)]
    public ENetDataFormat Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    private string _responseText = string.Empty;
    [NodeProperty(DisplayName = "回复文本", Group = "2.结果", Order = 10, ReadOnly = 1)]
    public string ResponseText
    {
        get => _responseText;
        set => SetProperty(ref _responseText, value);
    }

    private int _responseCount;
    [NodeProperty(DisplayName = "回复字节数", Group = "2.结果", Order = 11, ReadOnly = 1)]
    public int ResponseCount
    {
        get => _responseCount;
        set => SetProperty(ref _responseCount, value);
    }

    private byte[] _responseBytes = [];
    [NodeProperty(DisplayName = "回复字节", Group = "2.结果", Order = 12, IsDisplay = false)]
    public byte[] ResponseBytes
    {
        get => _responseBytes;
        set => SetProperty(ref _responseBytes, value);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ResponseBytes = [];
        ResponseCount = 0;
        ResponseText = string.Empty;

        byte[] request;
        try
        {
            request = NetDeviceHelper.ParseData(RequestText, Format);
        }
        catch (Exception ex)
        {
            throw await CreateFailureAsync("请求", null, ex, $"内容解析失败：{ex.Message}");
        }

        var device = GetDevice();
        if (!NetDeviceHelper.SupportsRequestReply(device))
            throw await CreateFailureAsync("请求", null, detail: $"{device.ProtocolType} {device.Role} 不支持请求-回复模式");

        var response = await RunDeviceOperationAsync(
            "请求",
            null,
            ct,
            currentDevice => currentDevice.SendAndReceiveAsync(request, ct),
            device: device);
        if (response is not { Length: > 0 })
            throw await CreateFailureAsync("请求", null, detail: "未收到有效回复");

        ResponseBytes = response;
        ResponseCount = response.Length;
        ResponseText = NetDeviceHelper.FormatData(response, Format);
        await NodeLog.InfoAsync($"请求 [{DeviceName}]：发送 {request.Length} 字节，收到 {ResponseCount} 字节");
    }
}
