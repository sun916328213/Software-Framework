using System.IO;
using System.IO.Ports;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Http;
using CommunicationNew.Protocols.Mitsubishi;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using CommunicationNew.Protocols.OpcUa;
using CommunicationNew.Protocols.Serial;
using CommunicationNew.Protocols.Socket;
using CommunicationNew.Protocols.WebApi;

namespace CommunicationTestApp.Services;

/// <summary>
/// 设备会话服务 —— 全部设备的生命周期所有者。
/// 通过 CommunicationManager 管理 CommunicationNew 设备，事件统一汇聚到 ILogService。
/// </summary>
public sealed class CommunicationSessionService : IDisposable
{
    /// <summary>接收显示格式（HEX / 文本），由工作台 HEX 开关统一控制收发。</summary>
    public static string ReceiveDisplayFormat { get; set; } = "HEX";

    private readonly ILogService _log;
    private readonly CommunicationManager _manager;
    private bool _disposed;

    public CommunicationSessionService(ILogService log)
    {
        _log = log;
        _manager = new CommunicationManager(CommunicationNew.Composition.CommunicationDefaults.CreateDefaultFactory());
    }

    /// <summary>当前会话内的全部设备</summary>
    public IReadOnlyCollection<ICommunicationDevice> Devices => _manager.Devices;

    /// <summary>创建指定协议默认参数的新设备并纳入管理。</summary>
    public ICommunicationDevice CreateDevice(ProtocolType protocol, DeviceRole role, string name)
    {
        var param = ProtocolCatalog.CreateParam(protocol, role);
        param.DeviceName = name;
        param.DeviceId = Guid.NewGuid().ToString("N");
        var device = _manager.CreateDevice(param);
        HookDevice(device);
        _log.Info("Session", $"创建设备 [{device.ProtocolType}] {name}");
        return device;
    }

    /// <summary>按自定义参数创建设备（对话框编辑后）。</summary>
    public ICommunicationDevice CreateDevice(CommunicationParam param)
    {
        var device = _manager.CreateDevice(param);
        HookDevice(device);
        _log.Info("Session", $"创建设备 [{device.ProtocolType}] {param.DeviceName}");
        return device;
    }

    /// <summary>移除并释放设备。</summary>
    public Task<bool> RemoveDeviceAsync(string deviceId) =>
        _manager.RemoveDeviceAsync(deviceId);

    /// <summary>连接会话内全部设备（按提交顺序执行，单设备失败不阻止其余设备）。</summary>
    public Task ConnectAllAsync(CancellationToken ct = default) =>
        _manager.ConnectAllAsync(ct);

    /// <summary>断开会话内全部设备。</summary>
    public Task DisconnectAllAsync(CancellationToken ct = default) =>
        _manager.DisconnectAllAsync(ct);

    /// <summary>取设备创建时的参数快照（用于持久化）。</summary>
    public CommunicationParam? GetParam(string deviceId)
    {
        var device = _manager.GetDevice(deviceId);
        return device is null ? null : SnapshotParam(device);
    }

    /// <summary>全部设备的参数快照（用于持久化）。</summary>
    public IReadOnlyList<CommunicationParam> GetAllParams()
    {
        var list = new List<CommunicationParam>();
        foreach (var device in _manager.Devices)
        {
            list.Add(SnapshotParam(device));
        }
        return list;
    }

    /// <summary>按参数重建设备并纳入管理（配置恢复入口）。</summary>
    public ICommunicationDevice RestoreDevice(CommunicationParam param)
    {
        var device = _manager.CreateDevice(param);
        HookDevice(device);
        _log.Info("Session", $"恢复设备 [{device.ProtocolType}] {param.DeviceName}");
        return device;
    }

    private static CommunicationParam SnapshotParam(ICommunicationDevice device) =>
        DeviceConfigStore.CloneParamFor(device);

    /// <summary>连接单个设备。</summary>
    public async Task<bool> ConnectDeviceAsync(ICommunicationDevice device, CancellationToken ct = default)
    {
        if (device is CommunicationNew.Abstractions.IConnection connection)
        {
            try
            {
                await connection.ConnectAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(device.DeviceName, $"连接失败: {ex.Message}");
                return false;
            }
        }

        return await device.ConnectAsync(ct);
    }

    /// <summary>断开单个设备。</summary>
    public async Task DisconnectDeviceAsync(ICommunicationDevice device, CancellationToken ct = default)
    {
        if (device is CommunicationNew.Abstractions.IConnection connection)
        {
            try
            {
                await connection.DisconnectAsync(ct);
            }
            catch (Exception ex)
            {
                _log.Warning(device.DeviceName, $"断开异常: {ex.Message}");
            }
            return;
        }

        await device.DisconnectAsync(ct);
    }

    private void HookDevice(ICommunicationDevice device)
    {
        device.ConnectionStateChanged += (_, e) =>
            _log.Info(device.DeviceName, e.IsConnected
                ? $"已连接 {(e.Message ?? string.Empty)}".Trim()
                : $"已断开 {(e.Message ?? string.Empty)}".Trim());

        device.DataReceived += (_, e) =>
            _log.Rx(device.DeviceName, FormatData(e.Data, e.RemoteAddress));

        device.ErrorOccurred += (_, e) =>
            _log.Error(device.DeviceName, e.Exception.Message);
    }

    private static string FormatData(byte[] data, string? remoteAddress)
    {
        string body;
        if (data.Length == 0)
        {
            body = string.Empty;
        }
        else if (ReceiveDisplayFormat == "文本")
        {
            body = System.Text.Encoding.UTF8.GetString(data);
        }
        else
        {
            body = BitConverter.ToString(data).Replace("-", " ");
        }

        var text = body.Length > 128 ? body[..128] + "…" : body;
        return string.IsNullOrEmpty(remoteAddress) ? text : $"{text}  ({remoteAddress})";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _manager.Dispose();
    }
}

/// <summary>协议目录：协议类型 → 参数构造、角色支持、地址提示等 UI 元数据。</summary>
public static class ProtocolCatalog
{
    public record ProtocolInfo(
        ProtocolType Protocol,
        string DisplayName,
        string Icon,
        bool SupportsServer,
        bool SupportsAddressRead,
        bool SupportsRaw,
        string AddressHint,
        string AddressRegexHint)
    {
        public DeviceRole DefaultRole => DeviceRole.Client;
    }

    public static ProtocolInfo Describe(ProtocolType protocol) => protocol switch
    {
        // 地址读写走 CommunicationNew IAddressReader；原始收发仅 TCP/UDP/Serial（字节流/数据报）。
        // HTTP/WebApi 是高层 adapter，不走原始收发 seam。
        ProtocolType.ModbusTcp => new(ProtocolType.ModbusTcp, "Modbus TCP", "\u2699", true, true, false,
            "HR0 / 30001 / 40001 (地址)", "地址如 HR0、IR0、C0、DI0 或 40001"),
        ProtocolType.ModbusRtu => new(ProtocolType.ModbusRtu, "Modbus RTU", "\u2699", true, true, false,
            "HR0 / 40001 (地址)", "地址如 HR0、C0、40001"),
        ProtocolType.MitsubishiMc => new(ProtocolType.MitsubishiMc, "三菱 MC", "\u25B8", false, true, false,
            "D100 / M10 (软元件)", "如 D100、M10、W0F0、R0"),
        ProtocolType.OpcUa => new(ProtocolType.OpcUa, "OPC UA", "\u25CE", true, true, false,
            "ns=2;s=Tag1 (节点ID)", "OPC UA NodeId，如 ns=2;s=Temperature"),
        ProtocolType.Tcp => new(ProtocolType.Tcp, "TCP Socket", "\u25A1", true, false, true,
            "-", "TCP 字节流原始收发"),
        ProtocolType.Udp => new(ProtocolType.Udp, "UDP Socket", "\u25A1", true, false, true,
            "-", "UDP 数据报收发"),
        ProtocolType.Serial => new(ProtocolType.Serial, "串口 Raw", "\u2248", false, false, true,
            "-", "原始串口收发"),
        ProtocolType.Http => new(ProtocolType.Http, "HTTP", "\u21C4", true, true, false,
            "/api/path (相对路径)", "GET 相对路径，如 /api/data"),
        ProtocolType.WebApi => new(ProtocolType.WebApi, "Web API", "\u21C4", true, true, false,
            "/api/v1/resource", "RESTful 路径"),
        _ => throw new NotSupportedException(protocol.ToString()),
    };

    /// <summary>
    /// 能力判定：生命周期一律走 <see cref="CommunicationNew.Abstractions.IConnection"/>；
    /// 地址能力以 <see cref="CommunicationNew.Abstractions.IAddressReader"/> + 协议目录双重判定，
    /// 避免 TCP/UDP/Serial 伪装地址读取；原始收发仅字节流/数据报协议。
    /// </summary>
    public static bool SupportsAddress(ICommunicationDevice device) =>
        device is CommunicationNew.Abstractions.IAddressReader
        && Describe(device.ProtocolType).SupportsAddressRead;

    public static bool SupportsRaw(ProtocolType protocol) =>
        Describe(protocol).SupportsRaw;

    /// <summary>列出全部可用协议信息（按常用度排序）。</summary>
    public static IReadOnlyList<ProtocolInfo> All() =>
    [
        Describe(ProtocolType.ModbusTcp),
        Describe(ProtocolType.ModbusRtu),
        Describe(ProtocolType.MitsubishiMc),
        Describe(ProtocolType.OpcUa),
        Describe(ProtocolType.Tcp),
        Describe(ProtocolType.Udp),
        Describe(ProtocolType.Serial),
        Describe(ProtocolType.Http),
        Describe(ProtocolType.WebApi),
    ];

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

    /// <summary>可用的串口名列表。</summary>
    public static string[] GetSerialPortNames()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch
        {
            return [];
        }
    }
}
