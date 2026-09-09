using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Http;
using CommunicationNew.Protocols.Mitsubishi;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using CommunicationNew.Protocols.OpcUa;
using CommunicationNew.Protocols.Serial;
using CommunicationNew.Protocols.Socket;
using CommunicationNew.Protocols.WebApi;
using System.Text;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 新通讯节点的共享逻辑：设备查找、连接/断开（含 <see cref="IConnection"/> 快捷路径，
/// 与 Communication.Workbench 的 CommunicationSessionService 保持一致）、文本/HEX 编解码。
/// </summary>
internal static class NetDeviceHelper
{
    private const string StagingMarker = ".__connecting__.";

    /// <summary>按设备名称查找管理器中的设备，找不到则抛异常（走节点容错路径）。</summary>
    public static ICommunicationDevice GetDeviceOrThrow(CommunicationManager manager, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            throw new InvalidOperationException("新通讯节点未配置设备名称，请先用「新通讯连接」创建设备。");

        var normalizedName = deviceName.Trim();
        return FindDevice(manager, normalizedName)
            ?? throw new InvalidOperationException($"新通讯设备 [{normalizedName}] 不存在，请先用「新通讯连接」创建。");
    }

    /// <summary>按用户可见名称查找设备；名称不区分大小写。</summary>
    public static ICommunicationDevice? FindDevice(CommunicationManager manager, string deviceName)
        => manager.Devices.FirstOrDefault(device =>
            string.Equals(device.DeviceName, deviceName.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>管理器中已创建的设备名称列表（供操作节点的设备下拉）。</summary>
    public static List<string> DeviceNames(CommunicationManager manager)
        => manager.Devices
            .Select(device => device.DeviceName)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !name.Contains(StagingMarker, StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>连接设备（IConnection 快捷路径 + 统一 bool 语义）。</summary>
    public static async Task<bool> ConnectAsync(ICommunicationDevice device, CancellationToken ct)
    {
        if (device is IConnection connection)
        {
            await connection.ConnectAsync(ct).ConfigureAwait(false);
            return device.IsConnected;
        }

        return await device.ConnectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>确保需要在线链路的操作不会在断开设备上运行。</summary>
    public static void EnsureConnected(ICommunicationDevice device)
    {
        if (!device.IsConnected)
            throw new InvalidOperationException($"新通讯设备 [{device.DeviceName}] 未连接，请先执行连接节点。");
    }

    /// <summary>是否支持按协议地址主动读取。</summary>
    public static bool SupportsAddressAccess(ICommunicationDevice device) => device.ProtocolType is
        ProtocolType.ModbusTcp or
        ProtocolType.ModbusRtu or
        ProtocolType.MitsubishiMc or
        ProtocolType.OpcUa or
        ProtocolType.Http or
        ProtocolType.WebApi;

    /// <summary>是否由 CommunicationNew 的接收循环发布被动数据。</summary>
    public static bool SupportsPassiveReceive(ICommunicationDevice device) => device.ProtocolType is
        ProtocolType.Tcp or ProtocolType.Udp or ProtocolType.Serial;

    /// <summary>是否支持原始报文发送。</summary>
    public static bool SupportsRawSend(ICommunicationDevice device) => device.ProtocolType is
        ProtocolType.Tcp or ProtocolType.Udp or ProtocolType.Serial;

    /// <summary>是否支持一次请求对应一次回复。</summary>
    public static bool SupportsRequestReply(ICommunicationDevice device) =>
        device.Role == DeviceRole.Client && SupportsRawSend(device);

    /// <summary>在替换现有设备前验证参数，避免无效配置破坏可用链路。</summary>
    public static void ValidateConnectionParam(CommunicationParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        if (string.IsNullOrWhiteSpace(param.DeviceName))
            throw new ArgumentException("设备名称不能为空。", nameof(param));
        if (param.TimeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(param), "通讯超时必须大于 0ms。");
        if (param.RetryCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(param), "通讯尝试次数必须大于 0。");
        if (param.RetryDelayMs < 0 || param.ReconnectIntervalMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(param), "重试间隔不能为负，重连间隔必须大于 0ms。");

        switch (param)
        {
            case SocketParam value:
                ValidateHostAndPort(value.ServerIP, value.Port);
                break;
            case ModbusTcpParam value:
                ValidateHostAndPort(value.ServerIP, value.Port);
                break;
            case ModbusRtuParam value:
                ValidateSerial(value.PortName, value.BaudRate, value.DataBits);
                break;
            case SerialParam value:
                ValidateSerial(value.PortName, value.BaudRate, value.DataBits);
                break;
            case McParam value:
                ValidateHostAndPort(value.ServerIP, value.Port);
                if (value.NetworkNo > 15)
                    throw new ArgumentOutOfRangeException(nameof(param), "MC 网络编号必须在 0-15 之间。");
                if (!string.Equals(value.FrameType, "3E", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(value.FrameType, "4E", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("MC 帧类型只能是 3E 或 4E。", nameof(param));
                break;
            case OpcUaParam value:
                ValidateAbsoluteUri(value.EndpointUrl, "opc.tcp");
                break;
            case HttpParam value:
                ValidateAbsoluteUri(value.BaseUrl, "http", "https");
                break;
            case WebApiParam value:
                ValidateAbsoluteUri(value.BaseUrl, "http", "https");
                break;
        }
    }

    /// <summary>断开设备（IConnection 快捷路径）。</summary>
    public static async Task DisconnectAsync(ICommunicationDevice device, CancellationToken ct)
    {
        if (device is IConnection connection)
        {
            await connection.DisconnectAsync(ct).ConfigureAwait(false);
            return;
        }

        await device.DisconnectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>把节点输入文本按格式转为待发送字节。</summary>
    public static byte[] ParseData(string? text, ENetDataFormat format)
        => format switch
        {
            ENetDataFormat.Hex => ParseHex(text),
            ENetDataFormat.UTF8 => Encoding.UTF8.GetBytes(text ?? string.Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的数据格式。"),
        };

    /// <summary>把收到/读到的字节按格式转为可显示文本。</summary>
    public static string FormatData(byte[]? data, ENetDataFormat format)
    {
        if (data == null || data.Length == 0) return string.Empty;
        return format switch
        {
            ENetDataFormat.Hex => string.Join(' ', data.Select(value => value.ToString("X2"))),
            ENetDataFormat.UTF8 => Encoding.UTF8.GetString(data),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的数据格式。"),
        };
    }

    /// <summary>限制日志中的报文长度，避免大报文占满日志和 UI。</summary>
    public static string Preview(string? text, int maxLength = 128)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : $"{text[..maxLength]}…";
    }

    private static byte[] ParseHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var source = text.AsSpan().Trim();
        var hex = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (char.IsWhiteSpace(c) || c is '-' or ',' or '_')
                continue;

            if (c == '0' && i + 1 < source.Length && source[i + 1] is 'x' or 'X')
            {
                i++;
                continue;
            }

            if (!Uri.IsHexDigit(c))
                throw new FormatException($"HEX 数据包含非法字符 '{c}'：[{text}]");

            hex.Append(c);
        }

        if (hex.Length % 2 != 0)
            throw new FormatException($"HEX 数据长度必须为偶数：[{text}]");
        if (hex.Length == 0)
            throw new FormatException($"HEX 数据不包含有效字节：[{text}]");

        return Convert.FromHexString(hex.ToString());
    }

    private static void ValidateHostAndPort(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("服务器地址不能为空。");
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "端口必须在 1-65535 之间。");
    }

    private static void ValidateSerial(string portName, int baudRate, int dataBits)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("串口号不能为空。");
        if (baudRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(baudRate), "波特率必须大于 0。");
        if (dataBits is < 5 or > 8)
            throw new ArgumentOutOfRangeException(nameof(dataBits), "数据位必须在 5-8 之间。");
    }

    private static void ValidateAbsoluteUri(string value, params string[] allowedSchemes)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !allowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"地址必须是完整的 {string.Join('/', allowedSchemes)} URL：[{value}]");
        }
    }
}
