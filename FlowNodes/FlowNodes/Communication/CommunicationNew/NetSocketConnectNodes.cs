using CommunicationNew.Core;
using CommunicationNew.Protocols.Socket;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>新通讯连接（TCP）：Socket TCP 客户端/服务端。</summary>
[FlowTool("新通讯连接（TCP）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 10, Description = "创建并连接 TCP 设备")]
internal class NetTcpConnectNode : NetConnectNodeBase
{
    public NetTcpConnectNode(CommunicationManager manager) : base(manager) { }

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
    public NetUdpConnectNode(CommunicationManager manager) : base(manager) { }

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

    protected override CommunicationParam BuildParam() => new SocketParam
    {
        ProtocolType = ProtocolType.Udp,
        Role = Role,
        ServerIP = ServerIP,
        Port = Port,
    };
}
