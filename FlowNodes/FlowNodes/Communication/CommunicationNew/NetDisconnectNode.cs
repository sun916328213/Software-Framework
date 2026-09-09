using CommunicationNew.Core;
using FlowEditor.Core;
using FlowEditor.Nodes;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 新通讯断开节点：断开指定设备的连接（设备保留在管理器中，可再次连接；
/// 彻底释放请用管理器 RemoveDevice，此处不断开即删，避免误释放共享设备）。
/// 失败语义：断开异常抛异常走容错路径。
/// </summary>
[FlowTool("新通讯断开", "LanDisconnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 20, Description = "断开 CommunicationNew 设备")]
internal class NetDisconnectNode : NetDeviceNodeBase
{
    public NetDisconnectNode(CommunicationManager manager) : base(manager) { }

    private bool _disconnected;
    /// <summary>断开结果（true 表示已断开）。</summary>
    [NodeProperty(DisplayName = "已断开", Group = "1.基础", Order = 1, ReadOnly = 1)]
    public bool Disconnected
    {
        get => _disconnected;
        set => SetProperty(ref _disconnected, value);
    }

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
