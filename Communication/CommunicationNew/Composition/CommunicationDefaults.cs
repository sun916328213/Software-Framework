using CommunicationNew.Core;
using CommunicationNew.Protocols.Http;
using CommunicationNew.Protocols.Mitsubishi;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using CommunicationNew.Protocols.OpcUa;
using CommunicationNew.Protocols.Serial;
using CommunicationNew.Protocols.Socket;
using CommunicationNew.Protocols.WebApi;

namespace CommunicationNew.Composition;

/// <summary>注册 CommunicationNew 内置协议适配器的组合入口。</summary>
public static class CommunicationDefaults
{
    /// <summary>创建包含全部内置协议注册、通过 Core seam 暴露的设备 factory。</summary>
    public static CommunicationNew.Core.ICommunicationDeviceFactory CreateDefaultFactory()
    {
        return CreateRegistry();
    }

    /// <summary>
    /// 创建使用旧 Composition 接口暴露的内置设备 factory。
    /// 新代码应改用 <see cref="CreateDefaultFactory"/>。
    /// </summary>
    [Obsolete("请使用 CreateDefaultFactory；旧返回类型将在后续主版本移除。")]
#pragma warning disable CS0618 // 方法签名必须保留旧返回类型，供现有二进制调用方迁移。
    public static ICommunicationDeviceFactory CreateFactory()
#pragma warning restore CS0618
    {
        // 默认注册表在迁移期同时实现新旧接口，因此此转换不会创建额外包装对象。
#pragma warning disable CS0618
        return (ICommunicationDeviceFactory)CreateRegistry();
#pragma warning restore CS0618
    }

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
