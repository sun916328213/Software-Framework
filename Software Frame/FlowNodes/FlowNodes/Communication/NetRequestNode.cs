// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetRequestNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes.Communication.CommunicationNew、FlowNodes、Communication.Core、Communication.Runtime.Core、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowNodes.Communication.CommunicationNew;
using FlowNodes;
using Communication.Core;
using Communication.Runtime.Core;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Communication;

/// <summary>向 TCP、UDP 或串口客户端发送请求，并等待一帧回复。</summary>
[FlowTool("新通讯请求并等待回复", "SwapHorizontal", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 51, Description = "向 TCP/UDP/串口客户端发送请求并等待回复")]
internal sealed class NetRequestNode : NetDeviceNodeBase
{
    /// <summary>
    /// 初始化 NetRequestNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、ParseData、CreateFailureAsync、GetDevice、SupportsRequestReply；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
