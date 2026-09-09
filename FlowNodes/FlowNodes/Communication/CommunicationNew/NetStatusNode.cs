using CommunicationNew.Core;
using CommunicationNew.Abstractions;
using FlowEditor.Core;
using FlowEditor.Nodes;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 新通讯状态节点：查询指定设备的连接状态与协议（常用于轮询等待、分支判断）。
/// 成功不抛异常；设备不存在则抛异常（配置错误应尽早暴露）。
/// </summary>
[FlowTool("新通讯状态", "InformationOutline", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 60, Description = "查询设备连接状态")]
internal class NetStatusNode : NetDeviceNodeBase
{
    public NetStatusNode(CommunicationManager manager) : base(manager) { }

    private bool _isConnected;
    /// <summary>是否已连接。</summary>
    [NodeProperty(DisplayName = "已连接", Group = "2.结果", Order = 10, ReadOnly = 1)]
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private string _protocolName = "";
    /// <summary>设备协议（如 ModbusTcp）。</summary>
    [NodeProperty(DisplayName = "协议", Group = "2.结果", Order = 11, ReadOnly = 1)]
    public string ProtocolName
    {
        get => _protocolName;
        set => SetProperty(ref _protocolName, value);
    }

    private string _roleName = "";
    [NodeProperty(DisplayName = "角色", Group = "2.结果", Order = 12, ReadOnly = 1)]
    public string RoleName
    {
        get => _roleName;
        set => SetProperty(ref _roleName, value);
    }

    private string _connectionState = "";
    [NodeProperty(DisplayName = "连接状态", Group = "2.结果", Order = 13, ReadOnly = 1, Description = "支持 IConnection 的设备显示精确状态")]
    public string ConnectionState
    {
        get => _connectionState;
        set => SetProperty(ref _connectionState, value);
    }

    public override Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        IsConnected = false;
        ProtocolName = string.Empty;
        RoleName = string.Empty;
        ConnectionState = string.Empty;
        var device = GetDevice(requireConnected: false);
        IsConnected = device.IsConnected;
        ProtocolName = device.ProtocolType.ToString();
        RoleName = device.Role.ToString();
        ConnectionState = device is IConnection connection
            ? connection.State.ToString()
            : (device.IsConnected ? "Connected" : "Disconnected");
        return Task.CompletedTask;
    }
}
