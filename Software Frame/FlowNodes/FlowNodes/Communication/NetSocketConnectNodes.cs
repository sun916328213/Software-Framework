// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetTcpConnectNode、NetUdpConnectNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、Communication.Core、Communication.Core.Protocols.Socket、Communication.Runtime.Protocols.Socket、FlowEditor.Controls.Core。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using Communication.Core;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Socket;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Communication;

/// <summary>新通讯连接（TCP）：Socket TCP 客户端/服务端。</summary>
[FlowTool("新通讯连接（TCP）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 10, Description = "创建并连接 TCP 设备")]
internal class NetTcpConnectNode : NetConnectNodeBase
{
    /// <summary>
    /// 初始化 NetTcpConnectNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public NetTcpConnectNode(ICommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.Tcp;
    protected override string DefaultDeviceName => "TCP1";

    private DeviceRole _role = DeviceRole.Client;
    [NodeProperty(DisplayName = "角色", Group = "1.基础", Order = 1, Description = "客户端主动连接，服务端被动监听")]
    public DeviceRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private string _serverIP = "127.0.0.1";
    [NodeProperty(DisplayName = "服务器IP", Group = "2.参数", Order = 10, Description = "客户端填目标地址")]
    public string ServerIP
    {
        get => _serverIP;
        set => SetProperty(ref _serverIP, value);
    }

    private int _port = 8080;
    [NodeProperty(DisplayName = "端口", Group = "2.参数", Order = 11)]
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private string _delimiter = "\r\n";
    [NodeProperty(DisplayName = "分包符", Group = "2.参数", Order = 12, Description = "TCP粘包分包符，置空表示不分包")]
    public string Delimiter
    {
        get => _delimiter;
        set => SetProperty(ref _delimiter, value);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildParam。
    /// 正文中的关键协作调用包括 IsNullOrEmpty；具体条件和先后次序见方法体。
    /// </summary>
    protected override CommunicationParam BuildParam() => new SocketParam
    {
        ProtocolType = ProtocolType.Tcp,
        Role = Role,
        ServerIP = ServerIP,
        Port = Port,
        Delimiter = string.IsNullOrEmpty(Delimiter) ? null : Delimiter,
    };
}

/// <summary>新通讯连接（UDP）：Socket UDP 客户端/服务端。</summary>
[FlowTool("新通讯连接（UDP）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 11, Description = "创建并连接 UDP 设备")]
internal class NetUdpConnectNode : NetConnectNodeBase
{
    /// <summary>
    /// 初始化 NetUdpConnectNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public NetUdpConnectNode(ICommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.Udp;
    protected override string DefaultDeviceName => "UDP1";

    private DeviceRole _role = DeviceRole.Client;
    [NodeProperty(DisplayName = "角色", Group = "1.基础", Order = 1, Description = "客户端主动连接，服务端被动监听")]
    public DeviceRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private string _serverIP = "127.0.0.1";
    [NodeProperty(DisplayName = "服务器IP", Group = "2.参数", Order = 10, Description = "客户端填目标地址")]
    public string ServerIP
    {
        get => _serverIP;
        set => SetProperty(ref _serverIP, value);
    }

    private int _port = 8080;
    [NodeProperty(DisplayName = "端口", Group = "2.参数", Order = 11)]
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildParam。
    /// </summary>
    protected override CommunicationParam BuildParam() => new SocketParam
    {
        ProtocolType = ProtocolType.Udp,
        Role = Role,
        ServerIP = ServerIP,
        Port = Port,
    };
}
