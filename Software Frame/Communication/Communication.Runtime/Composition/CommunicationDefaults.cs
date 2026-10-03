// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：CommunicationDefaults。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Runtime.Core、Communication.Core.Protocols.Http、Communication.Runtime.Protocols.Http、Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.Protocols.Http;
using Communication.Runtime.Protocols.Http;
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;
using Communication.Core.Protocols.Modbus.ModbusRtu;
using Communication.Runtime.Protocols.Modbus.ModbusRtu;
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus.ModbusTcp;
using Communication.Core.Protocols.OpcUa;
using Communication.Runtime.Protocols.OpcUa;
using Communication.Core.Protocols.Serial;
using Communication.Runtime.Protocols.Serial;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Socket;
using Communication.Core.Protocols.WebApi;
using Communication.Runtime.Protocols.WebApi;

namespace Communication.Runtime.Composition;

/// <summary>注册 CommunicationNew 内置协议适配器的组合入口。</summary>
public static class CommunicationDefaults
{
    /// <summary>创建包含全部内置协议注册、通过 Core seam 暴露的设备 factory。</summary>
    public static Communication.Core.ICommunicationDeviceFactory CreateDefaultFactory()
    {
        return CreateRegistry();
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateRegistry。
    /// 正文中的关键协作调用包括 SetFallback、Register；具体条件和先后次序见方法体。
    /// </summary>
    private static CommunicationDeviceRegistry CreateRegistry()
    {
        return new CommunicationDeviceRegistry()
            .Register<SocketParam>(CreateSocket)
            .Register<ModbusRtuParam>(parameter => parameter.Role == DeviceRole.Server
                ? new ModbusRtuServerDevice(parameter)
                : new ModbusRtuClientDevice(parameter))
            .Register<ModbusTcpParam>(parameter => parameter.Role == DeviceRole.Server
                ? new ModbusTcpServerDevice(parameter)
                : new ModbusTcpClientDevice(parameter))
            .Register<OpcUaParam>(parameter => parameter.Role == DeviceRole.Server
                ? new OpcUaServerDevice(parameter)
                : new OpcUaClientDevice(parameter))
            .Register<HttpParam>(parameter => parameter.Role == DeviceRole.Server
                ? new HttpServerDevice(parameter)
                : new HttpClientDevice(parameter))
            .Register<WebApiParam>(parameter => parameter.Role == DeviceRole.Server
                ? new WebApiServerDevice(parameter)
                : new WebApiClientDevice(parameter))
            .Register<McParam>(parameter => new McClientDevice(parameter))
            .Register<SerialParam>(parameter => new SerialPortDevice(parameter))
            .SetFallback(CreateLegacyParameter);
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateSocket。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    private static ICommunicationDevice CreateSocket(SocketParam parameter)
    {
        return parameter.ProtocolType switch
        {
            ProtocolType.Tcp => parameter.Role == DeviceRole.Server
                ? new TcpServerDevice(parameter)
                : new TcpClientDevice(parameter),
            ProtocolType.Udp => parameter.Role == DeviceRole.Server
                ? new UdpServerDevice(parameter)
                : new UdpClientDevice(parameter),
            _ => throw new ArgumentException(
                $"SocketParam 不支持协议 {parameter.ProtocolType}。",
                nameof(parameter)),
        };
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateLegacyParameter。
    /// 正文中的关键协作调用包括 CreateSocket、CopyCommon、Create、CreateDefaultFactory；具体条件和先后次序见方法体。
    /// </summary>
    private static ICommunicationDevice CreateLegacyParameter(CommunicationParam parameter)
    {
        return parameter.ProtocolType switch
        {
            ProtocolType.Tcp or ProtocolType.Udp => CreateSocket(CopyCommon(parameter, new SocketParam
            {
                ProtocolType = parameter.ProtocolType,
            })),
            ProtocolType.ModbusRtu => CreateDefaultFactory().Create(CopyCommon(parameter, new ModbusRtuParam())),
            ProtocolType.ModbusTcp => CreateDefaultFactory().Create(CopyCommon(parameter, new ModbusTcpParam())),
            ProtocolType.OpcUa => CreateDefaultFactory().Create(CopyCommon(parameter, new OpcUaParam())),
            ProtocolType.Http => CreateDefaultFactory().Create(CopyCommon(parameter, new HttpParam())),
            ProtocolType.WebApi => CreateDefaultFactory().Create(CopyCommon(parameter, new WebApiParam())),
            ProtocolType.MitsubishiMc => CreateDefaultFactory().Create(CopyCommon(parameter, new McParam())),
            ProtocolType.Serial => CreateDefaultFactory().Create(CopyCommon(parameter, new SerialParam())),
            _ => throw new NotSupportedException($"不支持协议类型 {parameter.ProtocolType}。"),
        };
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CopyCommon。
    /// </summary>
    private static TParameter CopyCommon<TParameter>(
        CommunicationParam source,
        TParameter destination)
        where TParameter : CommunicationParam
    {
        destination.ProtocolType = source.ProtocolType;
        destination.Role = source.Role;
        destination.DeviceName = source.DeviceName;
        destination.DeviceId = source.DeviceId;
        destination.TimeoutMs = source.TimeoutMs;
        destination.RetryCount = source.RetryCount;
        destination.RetryDelayMs = source.RetryDelayMs;
        destination.AutoReconnect = source.AutoReconnect;
        destination.ReconnectIntervalMs = source.ReconnectIntervalMs;
        destination.EventQueueCapacity = source.EventQueueCapacity;
        destination.RequestQueueCapacity = source.RequestQueueCapacity;
        destination.ServerRequestCapacity = source.ServerRequestCapacity;
        return destination;
    }
}
