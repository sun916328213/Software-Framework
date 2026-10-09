// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetDisconnectNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、Communication.Core、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
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
/// 新通讯断开节点：断开指定设备的连接（设备保留在管理器中，可再次连接；
/// 彻底释放请用管理器 RemoveDevice，此处不断开即删，避免误释放共享设备）。
/// 失败语义：断开异常抛异常走容错路径。
/// </summary>
[FlowTool("新通讯断开", "LanDisconnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 20, Description = "断开 CommunicationNew 设备")]
internal class NetDisconnectNode : NetDeviceNodeBase
{
    /// <summary>
    /// 初始化 NetDisconnectNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public NetDisconnectNode(ICommunicationManager manager) : base(manager) { }

    private bool _disconnected;
    /// <summary>断开结果（true 表示已断开）。</summary>
    [NodeProperty(DisplayName = "已断开", Group = "1.基础", Order = 1, ReadOnly = 1)]
    public bool Disconnected
    {
        get => _disconnected;
        set => SetProperty(ref _disconnected, value);
    }

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、RunDeviceOperationAsync、DisconnectAsync、GetDevice、CreateFailureAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Disconnected = false;
        await RunDeviceOperationAsync(
            "断开",
            null,
            ct,
            async device =>
            {
                await NetDeviceHelper.DisconnectAsync(device, ct);
                return true;
            },
            requireConnected: false);
        var device = GetDevice(requireConnected: false);
        Disconnected = !device.IsConnected;
        if (!Disconnected)
            throw await CreateFailureAsync("断开", null, detail: "设备仍处于连接状态");
        await NodeLog.InfoAsync($"已断开 [{DeviceName}]");
    }
}
