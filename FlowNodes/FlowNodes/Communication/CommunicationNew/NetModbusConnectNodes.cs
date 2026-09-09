using System.IO.Ports;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>新通讯连接（ModbusTCP）：Modbus TCP 客户端/服务端。</summary>
[FlowTool("新通讯连接（ModbusTCP）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 12, Description = "创建并连接 ModbusTCP 设备")]
internal class NetModbusTcpConnectNode : NetConnectNodeBase
{
    public NetModbusTcpConnectNode(CommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.ModbusTcp;
    protected override string DefaultDeviceName => "ModbusTCP1";

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

    private int _port = 502;
    [NodeProperty(DisplayName = "端口", Group = "2.参数", Order = 11, Description = "ModbusTCP 默认 502")]
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private byte _slaveId = 1;
    [NodeProperty(DisplayName = "从站ID", Group = "2.参数", Order = 12, Description = "客户端模式下为目标从站")]
    public byte SlaveId
    {
        get => _slaveId;
        set => SetProperty(ref _slaveId, value);
    }

    protected override CommunicationParam BuildParam() => new ModbusTcpParam
    {
        Role = Role,
        ServerIP = ServerIP,
        Port = Port,
        SlaveId = SlaveId,
    };
}

/// <summary>新通讯连接（ModbusRTU）：Modbus RTU（串口）客户端/服务端。</summary>
[FlowTool("新通讯连接（ModbusRTU）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 13, Description = "创建并连接 ModbusRTU 设备")]
internal class NetModbusRtuConnectNode : NetConnectNodeBase
{
    public NetModbusRtuConnectNode(CommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.ModbusRtu;
    protected override string DefaultDeviceName => "ModbusRTU1";

    private DeviceRole _role = DeviceRole.Client;
    [NodeProperty(DisplayName = "角色", Group = "1.基础", Order = 1)]
    public DeviceRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

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

    private byte _slaveId = 1;
    [NodeProperty(DisplayName = "从站ID", Group = "2.参数", Order = 15, Description = "客户端模式下为目标从站")]
    public byte SlaveId
    {
        get => _slaveId;
        set => SetProperty(ref _slaveId, value);
    }

    protected override CommunicationParam BuildParam() => new ModbusRtuParam
    {
        Role = Role,
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        StopBits = StopBits,
        Parity = Parity,
        SlaveId = SlaveId,
    };
}
