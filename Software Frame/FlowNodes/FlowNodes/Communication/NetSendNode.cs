// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetSendNode。成员说明可结合下方具体实现阅读。
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

/// <summary>向 TCP、UDP 或串口设备发送原始报文，不等待回复。</summary>
[FlowTool("新通讯发送", "Send", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 50, Description = "向 TCP/UDP/串口发送原始报文")]
internal sealed class NetSendNode : NetDeviceNodeBase
{
    /// <summary>
    /// 初始化 NetSendNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、ParseData、CreateFailureAsync、GetDevice、SupportsRawSend；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
