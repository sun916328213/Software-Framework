// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：CommunicationParamFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Http、Communication.Core.Protocols.Mitsubishi、Communication.Core.Protocols.Modbus.ModbusRtu、Communication.Core.Protocols.Modbus.ModbusTcp、Communication.Core.Protocols.OpcUa、Communication.Core.Protocols.Serial。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Http;
using Communication.Core.Protocols.Mitsubishi;
using Communication.Core.Protocols.Modbus.ModbusRtu;
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Core.Protocols.OpcUa;
using Communication.Core.Protocols.Serial;
using Communication.Core.Protocols.Socket;
using Communication.Core.Protocols.WebApi;

namespace Communication.Core;

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
