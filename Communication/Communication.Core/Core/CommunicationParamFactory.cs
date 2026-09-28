using CommunicationNew.Protocols.Http;
using CommunicationNew.Protocols.Mitsubishi;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using CommunicationNew.Protocols.OpcUa;
using CommunicationNew.Protocols.Serial;
using CommunicationNew.Protocols.Socket;
using CommunicationNew.Protocols.WebApi;

namespace CommunicationNew.Core;

/// <summary>
/// P11 Core seam：协议参数的唯一创建入口（纯配置描述，无 IO、无第三方 SDK）。
/// Controls 通过此工厂构造默认参数，保持 Core-only；Runtime 的设备注册表负责参数到设备的映射。
/// </summary>
public static class CommunicationParamFactory
{
    /// <summary>根据协议与角色构造默认参数。</summary>
    public static CommunicationParam CreateParam(ProtocolType protocol, DeviceRole role) => protocol switch
    {
        ProtocolType.ModbusTcp => new ModbusTcpParam { ProtocolType = protocol, Role = role },
        ProtocolType.ModbusRtu => new ModbusRtuParam { ProtocolType = protocol, Role = role },
        ProtocolType.MitsubishiMc => new McParam { ProtocolType = protocol, Role = role },
        ProtocolType.OpcUa => new OpcUaParam { ProtocolType = protocol, Role = role },
        ProtocolType.Tcp => new SocketParam { ProtocolType = protocol, Role = role },
        ProtocolType.Udp => new SocketParam { ProtocolType = protocol, Role = role },
        ProtocolType.Serial => new SerialParam { ProtocolType = protocol, Role = role },
        ProtocolType.Http => new HttpParam { ProtocolType = protocol, Role = role },
        ProtocolType.WebApi => new WebApiParam { ProtocolType = protocol, Role = role },
        _ => throw new NotSupportedException(protocol.ToString()),
    };
}
