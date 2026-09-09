using System.IO.Ports;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Mitsubishi;
using CommunicationNew.Protocols.Serial;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>新通讯连接（MC）：三菱 SLMP 客户端（默认免 MX Component）。</summary>
[FlowTool("新通讯连接（MC）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 14, Description = "创建并连接三菱MC设备")]
internal class NetMcConnectNode : NetConnectNodeBase
{
    public NetMcConnectNode(CommunicationManager manager) : base(manager) { }

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
    public NetSerialConnectNode(CommunicationManager manager) : base(manager) { }

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
