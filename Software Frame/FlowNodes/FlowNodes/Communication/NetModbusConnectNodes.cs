// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetModbusTcpConnectNode、NetModbusRtuConnectNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、System.IO.Ports、Communication.Core、Communication.Runtime.Core、Communication.Core.Protocols.Modbus.ModbusRtu、Communication.Runtime.Protocols.Modbus.ModbusRtu。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using System.IO.Ports;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.Protocols.Modbus.ModbusRtu;
using Communication.Runtime.Protocols.Modbus.ModbusRtu;
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus.ModbusTcp;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Communication;

/// <summary>新通讯连接（ModbusTCP）：Modbus TCP 客户端/服务端。</summary>
[FlowTool("新通讯连接（ModbusTCP）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 12, Description = "创建并连接 ModbusTCP 设备")]
internal class NetModbusTcpConnectNode : NetConnectNodeBase
{
    /// <summary>
    /// 初始化 NetModbusTcpConnectNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildParam。
    /// </summary>
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
    /// <summary>
    /// 初始化 NetModbusRtuConnectNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildParam。
    /// </summary>
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
