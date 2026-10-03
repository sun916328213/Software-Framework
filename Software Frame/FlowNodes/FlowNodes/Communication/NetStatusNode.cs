// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetStatusNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、Communication.Core、Communication.Runtime.Core、Communication.Core.Abstractions、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.Abstractions;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Communication;

/// <summary>
/// 新通讯状态节点：查询指定设备的连接状态与协议（常用于轮询等待、分支判断）。
/// 成功不抛异常；设备不存在则抛异常（配置错误应尽早暴露）。
/// </summary>
[FlowTool("新通讯状态", "InformationOutline", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 60, Description = "查询设备连接状态")]
internal class NetStatusNode : NetDeviceNodeBase
{
    /// <summary>
    /// 初始化 NetStatusNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、GetDevice、ToString；具体条件和先后次序见方法体。
    /// </summary>
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
