// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetMcConnectNode、NetSerialConnectNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、System.IO.Ports、Communication.Core、Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using System.IO.Ports;
using Communication.Core;
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;
using Communication.Core.Protocols.Serial;
using Communication.Runtime.Protocols.Serial;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Communication;

/// <summary>新通讯连接（MC）：三菱 SLMP 客户端（默认免 MX Component）。</summary>
[FlowTool("新通讯连接（MC）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 14, Description = "创建并连接三菱MC设备")]
internal class NetMcConnectNode : NetConnectNodeBase
{
    /// <summary>
    /// 初始化 NetMcConnectNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public NetMcConnectNode(ICommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.MitsubishiMc;
    protected override string DefaultDeviceName => "MC1";

    private string _serverIP = "192.168.1.1";
    [NodeProperty(DisplayName = "服务器IP", Group = "2.参数", Order = 10, Description = "PLC 的 IP 地址")]
    public string ServerIP
    {
        get => _serverIP;
        set => SetProperty(ref _serverIP, value);
    }

    private int _port = 5000;
    [NodeProperty(DisplayName = "端口", Group = "2.参数", Order = 11, Description = "MC TCP 默认 5000/4999")]
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private byte _networkNo;
    [NodeProperty(DisplayName = "网络编号", Group = "2.参数", Order = 12, Description = "0-15")]
    public byte NetworkNo
    {
        get => _networkNo;
        set => SetProperty(ref _networkNo, value);
    }

    private byte _stationNo = 0xFF;
    [NodeProperty(DisplayName = "PLC站号", Group = "2.参数", Order = 13, Description = "0-255")]
    public byte StationNo
    {
        get => _stationNo;
        set => SetProperty(ref _stationNo, value);
    }

    private string _frameType = "3E";
    [NodeProperty(DisplayName = "帧类型", Group = "2.参数", Order = 14, Description = "3E 或 4E")]
    public string FrameType
    {
        get => _frameType;
        set => SetProperty(ref _frameType, value);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildParam。
    /// </summary>
    protected override CommunicationParam BuildParam() => new McParam
    {
        ServerIP = ServerIP,
        Port = Port,
        NetworkNo = NetworkNo,
        StationNo = StationNo,
        FrameType = FrameType,
    };
}

/// <summary>新通讯连接（串口）：原始串口收发。</summary>
[FlowTool("新通讯连接（串口）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 15, Description = "创建并连接串口设备")]
internal class NetSerialConnectNode : NetConnectNodeBase
{
    /// <summary>
    /// 初始化 NetSerialConnectNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public NetSerialConnectNode(ICommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.Serial;
    protected override string DefaultDeviceName => "Serial1";

    private string _portName = "COM1";
    [NodeProperty(DisplayName = "串口号", Group = "2.参数", Order = 10, Description = "如 COM1")]
    public string PortName
    {
        get => _portName;
        set => SetProperty(ref _portName, value);
    }

    private int _baudRate = 9600;
    [NodeProperty(DisplayName = "波特率", Group = "2.参数", Order = 11)]
    public int BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    private int _dataBits = 8;
    [NodeProperty(DisplayName = "数据位", Group = "2.参数", Order = 12)]
    public int DataBits
    {
        get => _dataBits;
        set => SetProperty(ref _dataBits, value);
    }

    private StopBits _stopBits = StopBits.One;
    [NodeProperty(DisplayName = "停止位", Group = "2.参数", Order = 13)]
    public StopBits StopBits
    {
        get => _stopBits;
        set => SetProperty(ref _stopBits, value);
    }

    private Parity _parity = Parity.None;
    [NodeProperty(DisplayName = "校验位", Group = "2.参数", Order = 14)]
    public Parity Parity
    {
        get => _parity;
        set => SetProperty(ref _parity, value);
    }

    private string _delimiter = "\r\n";
    [NodeProperty(DisplayName = "分包符", Group = "2.参数", Order = 15, Description = "置空表示不自动分包")]
    public string Delimiter
    {
        get => _delimiter;
        set => SetProperty(ref _delimiter, value);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildParam。
    /// 正文中的关键协作调用包括 IsNullOrEmpty；具体条件和先后次序见方法体。
    /// </summary>
    protected override CommunicationParam BuildParam() => new SerialParam
    {
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        StopBits = StopBits,
        Parity = Parity,
        Delimiter = string.IsNullOrEmpty(Delimiter) ? null : Delimiter,
    };
}
