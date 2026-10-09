// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetWriteNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes.Communication.CommunicationNew、FlowNodes、Communication.Core、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowNodes.Communication.CommunicationNew;
using FlowNodes;
using Communication.Core;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Communication;

/// <summary>
/// 新通讯写入节点：按地址向设备写入数据。
/// 失败语义：写入未被确认抛异常走容错路径。
/// </summary>
[FlowTool("新通讯地址写入", "Upload", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 40, Description = "按地址写入 Modbus/MC/OPC/HTTP 数据")]
internal class NetWriteNode : NetDeviceNodeBase
{
    /// <summary>
    /// 初始化 NetWriteNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public NetWriteNode(ICommunicationManager manager) : base(manager) { }

    private string _address = "D100";
    [NodeProperty(DisplayName = "地址", Group = "1.基础", Order = 1, Description = "Modbus:HR0/40001；MC:D100/M10；OPC:ns=2;s=Tag；HTTP:/api/data")]
    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    private string _dataText = "";
    [NodeProperty(DisplayName = "写入内容", Group = "1.基础", Order = 2, Description = "可连线输入：上游文本写入此端口即覆盖本属性")]
    public string DataText
    {
        get => _dataText;
        set => SetProperty(ref _dataText, value);
    }

    private ENetDataFormat _format = ENetDataFormat.UTF8;
    [NodeProperty(DisplayName = "内容格式", Group = "1.基础", Order = 3, Description = "写入内容的解析格式")]
    public ENetDataFormat Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    private bool _writeResult;
    /// <summary>写入结果（true 表示已确认）。</summary>
    [NodeProperty(DisplayName = "写入结果", Group = "2.结果", Order = 10, ReadOnly = 1)]
    public bool WriteResult
    {
        get => _writeResult;
        set => SetProperty(ref _writeResult, value);
    }

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、IsNullOrWhiteSpace、CreateFailureAsync、ParseData、GetDevice；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        WriteResult = false;
        if (string.IsNullOrWhiteSpace(Address))
            throw await CreateFailureAsync("写入", null, detail: "地址不能为空");

        byte[] data;
        try
        {
            data = NetDeviceHelper.ParseData(DataText, Format);
        }
        catch (Exception ex)
        {
            throw await CreateFailureAsync("写入", Address, ex, $"内容解析失败：{ex.Message}");
        }

        var device = GetDevice();
        if (!NetDeviceHelper.SupportsAddressAccess(device))
        {
            throw await CreateFailureAsync(
                "写入",
                Address,
                detail: $"{device.ProtocolType} 不支持按地址写入，请使用「新通讯发送」节点");
        }

        WriteResult = await RunDeviceOperationAsync(
            "写入",
            Address,
            ct,
            currentDevice => currentDevice.WriteAsync(Address.Trim(), data, ct),
            device: device);

        if (!WriteResult)
        {
            throw await CreateFailureAsync("写入", Address, detail: "写入未被确认");
        }
        await NodeLog.InfoAsync($"写入 [{DeviceName}] {Address}：{data.Length} 字节");
    }
}
