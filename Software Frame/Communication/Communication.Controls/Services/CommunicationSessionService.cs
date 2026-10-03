// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：CommunicationSessionService、ProtocolCatalog、ProtocolInfo。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Controls、System.IO、System.IO.Ports、System.Threading.Channels、Communication.Core.Abstractions、Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Controls;
using System.IO;
using System.IO.Ports;
using System.Threading.Channels;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using MaterialDesignThemes.Wpf;

namespace Communication.Controls.Services;

/// <summary>
/// 设备会话服务 —— 全部设备的生命周期所有者。
/// 通过 CommunicationManager 管理 CommunicationNew 设备，事件统一汇聚到 ILogService。
/// </summary>
public sealed class CommunicationSessionService : IDisposable, IAsyncDisposable
{
    private readonly ILogService _log;
    private readonly ICommunicationManager _manager;
    private readonly Dictionary<string, CancellationTokenSource> _receiveDrains = [];
    private readonly object _drainGate = new();
    private bool _disposed;

    /// <summary>
    /// 接收显示格式（HEX / 文本），由工作台 HEX 开关统一控制收发。
    /// 挂在会话服务（单例）上而不是静态字段，设备事件回调与界面开关走同一份状态。
    /// </summary>
    public string ReceiveDisplayFormat { get; set; } = "HEX";

    /// <summary>
    /// 初始化 CommunicationSessionService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public CommunicationSessionService(ILogService log, ICommunicationManager manager)
    {
        _log = log;
        _manager = manager;
    }

    /// <summary>当前会话内的全部设备</summary>
    public IReadOnlyCollection<ICommunicationDevice> Devices => _manager.Devices;

    /// <summary>
    /// 任一设备被动收到数据（原始字节 + 对端地址），在 UI 线程之外触发。
    /// 统一记入通讯日志，界面只看日志一处。
    /// </summary>
    public event Action<ICommunicationDevice, byte[], string?>? DataObserved;

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
    public async Task<bool> RemoveDeviceAsync(string deviceId)
    {
        var device = _manager.GetDevice(deviceId);
        if (device is not null)
        {
            StopReceiveDrain(device);
        }

        return await _manager.RemoveDeviceAsync(deviceId);
    }

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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SnapshotParam。
    /// 正文中的关键协作调用包括 CloneParamFor；具体条件和先后次序见方法体。
    /// </summary>
    private static CommunicationParam SnapshotParam(ICommunicationDevice device) =>
        DeviceConfigStore.CloneParamFor(device);

    /// <summary>连接单个设备。</summary>
    public async Task<bool> ConnectDeviceAsync(ICommunicationDevice device, CancellationToken ct = default)
    {
        if (device is IConnection connection)
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
        if (device is IConnection connection)
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 HookDevice。
    /// 正文中的关键协作调用包括 Info、IsNullOrWhiteSpace、StartReceiveDrain、StopReceiveDrain、ObserveData；具体条件和先后次序见方法体。
    /// </summary>
    private void HookDevice(ICommunicationDevice device)
    {
        device.ConnectionStateChanged += (_, e) =>
        {
            // 协议层的消息自带"已连接/已断开"，再前缀一次就成了"已连接 已连接 …"。
            _log.Info(device.DeviceName, string.IsNullOrWhiteSpace(e.Message)
                ? (e.IsConnected ? "已连接" : "已断开")
                : e.Message);

            // 收件箱按连接代际存在：连上才开始排空，断开就收尾。
            if (e.IsConnected)
            {
                StartReceiveDrain(device);
            }
            else
            {
                StopReceiveDrain(device);
            }
        };

        if (device is IPeerConnectionSource peerSource)
        {
            peerSource.PeerConnectionChanged += (_, e) =>
            {
                var action = e.IsConnected ? "已连接" : "已断开";
                var remote = string.IsNullOrWhiteSpace(e.RemoteAddress) ? "未知对端" : e.RemoteAddress;
                _log.Info(device.DeviceName, $"客户端 {remote} {action}，当前在线 {e.ActivePeerCount}");
            };
        }

        // 有被动收件箱的设备必须持续消费，否则队列写满会终止接收循环（表现为再也收不到、发不出）；
        // 这类设备的原始字节已经从收件箱记过一次日志，不能再订阅 DataReceived（分隔符兼容帧会重复）。
        if (device is not IReceiveSource<ReceivedPacket>)
        {
            device.DataReceived += (_, e) => ObserveData(device, e.Data, e.RemoteAddress);
        }

        device.ErrorOccurred += (_, e) =>
            _log.Error(device.DeviceName, e.Exception.Message);
    }

    /// <summary>为当前连接代际启动收件箱排空循环（同一设备只跑一个）。</summary>
    private void StartReceiveDrain(ICommunicationDevice device)
    {
        if (device is not IReceiveSource<ReceivedPacket> source)
        {
            return;
        }

        lock (_drainGate)
        {
            if (_disposed || _receiveDrains.ContainsKey(device.DeviceId))
            {
                return;
            }

            var cts = new CancellationTokenSource();
            _receiveDrains[device.DeviceId] = cts;
            _ = Task.Run(() => DrainReceiveAsync(device, source, cts));
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StopReceiveDrain。
    /// 正文中的关键协作调用包括 TryGetValue、Remove、Cancel；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    private void StopReceiveDrain(ICommunicationDevice device)
    {
        CancellationTokenSource? cts;
        lock (_drainGate)
        {
            if (!_receiveDrains.TryGetValue(device.DeviceId, out cts))
            {
                return;
            }

            _receiveDrains.Remove(device.DeviceId);
        }

        cts.Cancel();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 DrainReceiveAsync。
    /// 正文中的关键协作调用包括 ReceiveAsync、ToArray、ObserveData、TryGetValue、ReferenceEquals；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task DrainReceiveAsync(
        ICommunicationDevice device,
        IReceiveSource<ReceivedPacket> source,
        CancellationTokenSource cts)
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var packet = await source.ReceiveAsync(cts.Token);
                var data = packet.Data.ToArray();
                ObserveData(device, data, packet.RemoteAddress);
            }
        }
        catch (OperationCanceledException)
        {
            // 主动断开与会话释放。
        }
        catch (ChannelClosedException)
        {
            // 连接代际结束：收件箱完成读取，下一次连上会重启本循环。
        }
        finally
        {
            // 只清理自己那一份，避免与重连后新启动的循环互踩。
            lock (_drainGate)
            {
                if (_receiveDrains.TryGetValue(device.DeviceId, out var current)
                    && ReferenceEquals(current, cts))
                {
                    _receiveDrains.Remove(device.DeviceId);
                }
            }

            cts.Dispose();
        }
    }

    /// <summary>收到数据的统一出口：记通讯日志，界面从日志看全文。</summary>
    private void ObserveData(ICommunicationDevice device, byte[] data, string? remoteAddress)
    {
        if (_disposed) return;
        _log.Rx(device.DeviceName, FormatData(data, remoteAddress));
        DataObserved?.Invoke(device, data, remoteAddress);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FormatData。
    /// 正文中的关键协作调用包括 GetString、Replace、ToString、IsNullOrEmpty；具体条件和先后次序见方法体。
    /// </summary>
    private string FormatData(byte[] data, string? remoteAddress)
    {
        string body;
        if (data.Length == 0)
        {
            body = "(空响应)";
        }
        else if (ReceiveDisplayFormat == "文本")
        {
            body = System.Text.Encoding.UTF8.GetString(data);
        }
        else
        {
            body = BitConverter.ToString(data).Replace("-", " ");
        }

        return string.IsNullOrEmpty(remoteAddress) ? body : $"{body}  ({remoteAddress})";
    }

    /// <summary>Stops passive drains before asynchronously releasing the owned manager.</summary>
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 Cancel、Clear、ConfigureAwait、DisposeAsync、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_drainGate)
        {
            foreach (var cts in _receiveDrains.Values) cts.Cancel();
            _receiveDrains.Clear();
        }
        if (_manager is IAsyncDisposable disposable) await disposable.DisposeAsync().ConfigureAwait(false);
        else _manager.Dispose();
    }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 ToList、Cancel、Clear、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var cts in _receiveDrains.Values.ToList())
        {
            cts.Cancel();
        }

        _receiveDrains.Clear();
        _manager.Dispose();
    }
}

/// <summary>协议目录：协议类型 → 参数构造、角色支持、地址提示等 UI 元数据。</summary>
public static class ProtocolCatalog
{
    /// <summary>
    /// ProtocolInfo 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
    /// </summary>
    public record ProtocolInfo(
        ProtocolType Protocol,
        string DisplayName,
        PackIconKind Icon,
        bool SupportsServer,
        bool SupportsAddressRead,
        bool SupportsRaw,
        string AddressHint,
        string AddressRegexHint)
    {
        public DeviceRole DefaultRole => DeviceRole.Client;

        /// <summary>选型提示：这台协议能不能跟"网络调试助手"这类任意字节对端对话。</summary>
        public string UsageHint => SupportsRaw
            ? "原始字节收发：对端可以是任意 TCP / UDP / 串口设备（含网络调试助手）"
            : SupportsAddressRead
                ? $"只按 {DisplayName} 帧语义读写：对端必须说同一种协议，收到其它字节不会显示，也不提供原始发送"
                : $"只按 {DisplayName} 语义工作：不提供原始字节收发";
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Describe。
    /// 正文中的关键协作调用包括 SupportsServer、SupportsAddressRead、SupportsRaw、ToString；具体条件和先后次序见方法体。
    /// </summary>
    public static ProtocolInfo Describe(ProtocolType protocol) => protocol switch
    {
        // 地址读写走 CommunicationNew IAddressReader；原始收发仅 TCP/UDP/Serial（字节流/数据报）。
        // HTTP/WebApi 是高层 adapter，不走原始收发 seam。
        // Modbus 地址按一基解释（HR1 才是首个保持寄存器），提示里给 HR0 会让用户照着填一个必然报错的地址。
        // P11：能力布尔值以 Core.ProtocolCapabilities 为准，UI 只保留显示名/图标/提示。
        ProtocolType.ModbusTcp => new(ProtocolType.ModbusTcp, "Modbus TCP", PackIconKind.CogOutline, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "HR1 / 40001 / 30001 (地址)", "地址一基：HR1 = 40001 = 首个保持寄存器；写成 HR0 会被判为非法地址"),
        ProtocolType.ModbusRtu => new(ProtocolType.ModbusRtu, "Modbus RTU", PackIconKind.Cog, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "HR1 / 40001 (地址)", "地址一基：HR1 = 40001 = 首个保持寄存器"),
        ProtocolType.MitsubishiMc => new(ProtocolType.MitsubishiMc, "三菱 MC", PackIconKind.Chip, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "D100 / M10 (软元件)", "如 D100、M10、W0F0、R0"),
        ProtocolType.OpcUa => new(ProtocolType.OpcUa, "OPC UA", PackIconKind.Sitemap, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "ns=2;s=Tag1 (节点ID)", "OPC UA NodeId，如 ns=2;s=Temperature"),
        ProtocolType.Tcp => new(ProtocolType.Tcp, "TCP Socket", PackIconKind.Ethernet, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "-", "TCP 字节流原始收发"),
        ProtocolType.Udp => new(ProtocolType.Udp, "UDP Socket", PackIconKind.AccessPointNetwork, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "-", "UDP 数据报收发"),
        ProtocolType.Serial => new(ProtocolType.Serial, "串口 Raw", PackIconKind.SerialPort, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "-", "原始串口收发"),
        ProtocolType.Http => new(ProtocolType.Http, "HTTP", PackIconKind.Web, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "/api/path (相对路径)", "GET 相对路径，如 /api/data"),
        ProtocolType.WebApi => new(ProtocolType.WebApi, "Web API", PackIconKind.Api, ProtocolCapabilities.SupportsServer(protocol), ProtocolCapabilities.SupportsAddressRead(protocol), ProtocolCapabilities.SupportsRaw(protocol),
            "/api/v1/resource", "RESTful 路径"),
        _ => throw new NotSupportedException(protocol.ToString()),
    };

    /// <summary>
    /// 能力判定：生命周期一律走 <see cref="IConnection"/>；
    /// 地址能力以 <see cref="IAddressReader"/> + 协议目录双重判定，
    /// 避免 TCP/UDP/Serial 伪装地址读取；原始收发仅字节流/数据报协议。
    /// </summary>
    public static bool SupportsAddress(ICommunicationDevice device) =>
        device is IAddressReader && ProtocolCapabilities.SupportsAddressRead(device.ProtocolType);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SupportsRaw。
    /// 正文中的关键协作调用包括 SupportsRaw；具体条件和先后次序见方法体。
    /// </summary>
    public static bool SupportsRaw(ProtocolType protocol) =>
        ProtocolCapabilities.SupportsRaw(protocol);

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

    /// <summary>根据协议与角色构造默认参数（P11：委托 Core 工厂；Controls 不直接 new Runtime 参数之外的实现）。</summary>
    public static CommunicationParam CreateParam(ProtocolType protocol, DeviceRole role) =>
        CommunicationParamFactory.CreateParam(protocol, role);

    /// <summary>
    /// 协议参数白名单（顺序即表单显示顺序）：只列 CommunicationNew adapter 真正读取的字段。
    /// 未被任何 adapter 读取的字段（心跳、HTTP 长连接、Modbus 数据区大小等）不进表单，摆出来只是伪配置。
    /// </summary>
    public static IReadOnlyList<string> VisibleProtocolProperties(CommunicationParam param) =>
        (param.ProtocolType, param.Role) switch
        {
            (ProtocolType.ModbusTcp, DeviceRole.Server) => ["Port", "SlaveId"],
            (ProtocolType.ModbusTcp, _) => ["ServerIP", "Port", "SlaveId"],
            (ProtocolType.ModbusRtu, _) =>
                ["PortName", "BaudRate", "DataBits", "StopBits", "Parity", "SlaveId"],
            (ProtocolType.MitsubishiMc, _) =>
                ["Backend", "ServerIP", "Port", "FrameType", "NetworkNo", "StationNo", "ModuleNo",
                    "MxLogicalStationNumber", "MxPassword"],
            (ProtocolType.OpcUa, DeviceRole.Server) =>
                ["EndpointUrl", "ApplicationName", "ServerNamespaceUri", "SecurityMode", "SecurityPolicy",
                    "PkiRootPath", "AutoAcceptUntrustedCertificates", "MinimumCertificateKeySize"],
            (ProtocolType.OpcUa, _) =>
                ["EndpointUrl", "SecurityMode", "SecurityPolicy", "UserName", "Password", "OperationTimeout",
                    "PkiRootPath", "AutoAcceptUntrustedCertificates", "MinimumCertificateKeySize"],
            (ProtocolType.Tcp, DeviceRole.Server) =>
                ["LocalIP", "Port", "MaxConnections", "BufferSize", "Encoding", "Delimiter"],
            (ProtocolType.Tcp, _) => ["ServerIP", "Port", "Encoding", "Delimiter"],
            (ProtocolType.Udp, DeviceRole.Server) => ["LocalIP", "Port"],
            (ProtocolType.Udp, _) => ["ServerIP", "Port", "LocalIP", "LocalPort"],
            (ProtocolType.Serial, _) =>
                ["PortName", "BaudRate", "DataBits", "StopBits", "Parity", "Handshake", "BufferSize",
                    "Delimiter", "DtrEnable", "RtsEnable"],
            (ProtocolType.Http, DeviceRole.Server) => ["BaseUrl"],
            (ProtocolType.Http, _) => ["BaseUrl", "ContentType", "IgnoreSslErrors"],
            (ProtocolType.WebApi, DeviceRole.Server) => ["BaseUrl", "ApiPrefix"],
            (ProtocolType.WebApi, _) =>
                ["BaseUrl", "ApiPrefix", "ContentType", "BearerToken", "ApiKey", "ApiKeyHeaderName",
                    "OperationTimeoutMs", "IgnoreSslErrors"],
            _ => [],
        };

    /// <summary>通用参数白名单：按"当前协议与角色是否会读到"筛选，而不是把基类字段全铺出来。</summary>
    public static IReadOnlyList<string> VisibleCommonProperties(CommunicationParam param)
    {
        var names = new List<string> { nameof(CommunicationParam.TimeoutMs) };

        // 重试只包在带地址语义的读操作外面（CommunicationDeviceBase 的读重试）。
        if (Describe(param.ProtocolType).SupportsAddressRead)
        {
            names.Add(nameof(CommunicationParam.RetryCount));
            names.Add(nameof(CommunicationParam.RetryDelayMs));
        }

        // 只有带重连 supervisor 的面向连接客户端会读到重连配置。
        if (param.Role == DeviceRole.Client
            && param.ProtocolType is ProtocolType.Tcp or ProtocolType.ModbusTcp
                or ProtocolType.ModbusRtu or ProtocolType.MitsubishiMc)
        {
            names.Add(nameof(CommunicationParam.AutoReconnect));
            names.Add(nameof(CommunicationParam.ReconnectIntervalMs));
        }

        // 只有连续字节流/数据报设备才有事件队列与被动收件箱。
        if (param.ProtocolType is ProtocolType.Tcp or ProtocolType.Udp or ProtocolType.Serial)
        {
            names.Add(nameof(CommunicationParam.EventQueueCapacity));
            names.Add(nameof(CommunicationParam.ReceiveQueueCapacity));
        }

        // 请求队列只存在于自行排队请求的客户端 adapter。
        if (param.Role == DeviceRole.Client
            && param.ProtocolType is ProtocolType.ModbusTcp or ProtocolType.ModbusRtu
                or ProtocolType.MitsubishiMc)
        {
            names.Add(nameof(CommunicationParam.RequestQueueCapacity));
        }

        if (param.Role == DeviceRole.Server)
        {
            names.Add(nameof(CommunicationParam.ServerRequestCapacity));
        }

        return names;
    }

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
