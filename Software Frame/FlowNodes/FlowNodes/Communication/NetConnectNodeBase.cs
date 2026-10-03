// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetConnectNodeBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、Communication.Core、Communication.Runtime.Core、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、WPFCustomControls.PropertyGrid.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using Communication.Core;
using Communication.Runtime.Core;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Communication;

/// <summary>
/// 新通讯连接基类：承载设备名、超时重连、建设备（同名复用）与连接的公共逻辑。
/// 各协议子类只需声明 <see cref="FlowEditor.Core.FlowToolAttribute"/>、
/// 本协议参数属性与 <see cref="BuildParam"/>，面板上只出现本协议的参数。
/// </summary>
internal abstract class NetConnectNodeBase : NodeModel
{
    protected readonly CommunicationManager Manager;

    /// <summary>
    /// 初始化 NetConnectNodeBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    protected NetConnectNodeBase(CommunicationManager manager)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <summary>本节点负责的协议（子类定死，面板不显示，只用于建参与校验）。</summary>
    public abstract ProtocolType Protocol { get; }

    /// <summary>子类默认设备名（面板初始值，同协议多设备时改名即可）。</summary>
    protected virtual string DefaultDeviceName => "NetDevice1";

    // ── 1.基础 ──

    private string _deviceName = "";
    [NodeProperty(DisplayName = "设备名称", Group = "1.基础", Order = 0, Description = "管理器内唯一，后续读写节点凭此名引用")]
    public string DeviceName
    {
        get => string.IsNullOrEmpty(_deviceName) ? DefaultDeviceName : _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    // ── 设备级容错（与 NodeModel 的流程执行容错分开）──

    private bool _autoReconnect = true;
    [NodeProperty(DisplayName = "自动重连", Group = "1.基础", Order = 2)]
    public bool AutoReconnect
    {
        get => _autoReconnect;
        set => SetProperty(ref _autoReconnect, value);
    }

    private int _communicationTimeoutMs = 3000;
    [NodeProperty(DisplayName = "通讯超时(ms)", Group = "1.基础", Order = 3, Min = 1, Description = "CommunicationNew 单次连接/读写超时；与节点执行超时分开")]
    public int CommunicationTimeoutMs
    {
        get => _communicationTimeoutMs;
        set => SetProperty(ref _communicationTimeoutMs, Math.Clamp(value, 1, 3_600_000));
    }

    private int _communicationRetryCount = 3;
    [NodeProperty(DisplayName = "通讯尝试次数", Group = "1.基础", Order = 4, Min = 1, Description = "CommunicationNew 库内总尝试次数（含首次）")]
    public int CommunicationRetryCount
    {
        get => _communicationRetryCount;
        set => SetProperty(ref _communicationRetryCount, Math.Clamp(value, 1, 10));
    }

    private int _communicationRetryDelayMs = 1000;
    [NodeProperty(DisplayName = "通讯重试间隔(ms)", Group = "1.基础", Order = 5, Min = 0)]
    public int CommunicationRetryDelayMs
    {
        get => _communicationRetryDelayMs;
        set => SetProperty(ref _communicationRetryDelayMs, Math.Clamp(value, 0, 60_000));
    }

    private int _reconnectIntervalMs = 3000;
    [NodeProperty(DisplayName = "重连间隔(ms)", Group = "1.基础", Order = 6, Min = 1)]
    public int ReconnectIntervalMs
    {
        get => _reconnectIntervalMs;
        set => SetProperty(ref _reconnectIntervalMs, Math.Clamp(value, 1, 3_600_000));
    }

    // ── 输出 ──

    private bool _isConnected;
    [NodeProperty(DisplayName = "已连接", Group = "1.基础", Order = 1, ReadOnly = 1, Description = "本次执行后的连接状态")]
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、IsNullOrWhiteSpace、Trim、BuildParam、FillCommon；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IsConnected = false;

        if (string.IsNullOrWhiteSpace(DeviceName))
            throw new InvalidOperationException("新通讯连接失败：设备名称为空。");

        DeviceName = DeviceName.Trim();

        CommunicationParam newParam;
        try
        {
            newParam = BuildParam();
            FillCommon(newParam);
            NetDeviceHelper.ValidateConnectionParam(newParam);
        }
        catch (Exception ex)
        {
            var message = $"新通讯连接失败 [{DeviceName}]：参数无效：{ex.Message}";
            await NodeLog.ErrorAsync(message);
            throw new InvalidOperationException(message, ex);
        }

        var existing = NetDeviceHelper.FindDevice(Manager, DeviceName);
        if (existing != null)
        {
            if (existing.ProtocolType != Protocol)
            {
                throw new InvalidOperationException(
                    $"新通讯连接失败：已存在同名设备 [{DeviceName}] 但协议为 {existing.ProtocolType}，与本次 {Protocol} 不一致，请改名。");
            }
        }

        // 先以临时名称建立新链路，成功后再替换旧设备。
        // 这样参数错误或网络抖动不会破坏原有可用连接。
        newParam.DeviceName = $"{DeviceName}.__connecting__.{Guid.NewGuid():N}";
        var device = await CreateDeviceAsync(newParam);
        if (NetDeviceHelper.SupportsPassiveReceive(device))
            NetReceiveInbox.Attach(device);
        try
        {
            var ok = await NetDeviceHelper.ConnectAsync(device, ct);
            if (!ok)
                throw new InvalidOperationException("未能建立连接");

            if (existing != null)
            {
                try
                {
                    await Manager.RemoveDeviceAsync(existing.DeviceId);
                }
                catch (Exception ex)
                {
                    // RemoveDeviceAsync 在清理前已从管理器移除旧设备；
                    // 新链路可继续使用，但要保留清理故障记录。
                    await NodeLog.WarnAsync($"旧设备清理异常 [{DeviceName}]：{ex.Message}");
                }
            }

            device.DeviceName = DeviceName;
            IsConnected = device.IsConnected;
            if (!IsConnected)
                throw new InvalidOperationException("建连返回成功，但设备状态仍为未连接");

        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RemoveCandidateAsync(device);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // 库内部超时（如 TCP 建连超时）借 TaskCanceledException 表达，此时并未取消：
            // 转为明确的失败，避免面板出现无意义的“A task was canceled”。
            await RemoveCandidateAsync(device);
            var msg = $"新通讯连接失败 [{DeviceName}]：连接超时（{CommunicationTimeoutMs}ms），请检查目标地址/端口与网络";
            await NodeLog.ErrorAsync(msg);
            throw new InvalidOperationException(msg, ex);
        }
        catch (Exception ex)
        {
            await RemoveCandidateAsync(device);
            var msg = $"新通讯连接失败 [{DeviceName}]：{ex.Message}";
            await NodeLog.ErrorAsync(msg);
            throw new InvalidOperationException(msg, ex);
        }

        await NodeLog.InfoAsync($"连接成功 [{DeviceName}]");
    }

    /// <summary>子类构造本协议的参数（含协议特有字段）。通用字段由基类统一回填。</summary>
    protected abstract CommunicationParam BuildParam();

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateDeviceAsync。
    /// 正文中的关键协作调用包括 CreateDevice、ErrorAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task<ICommunicationDevice> CreateDeviceAsync(CommunicationParam param)
    {
        try
        {
            var device = Manager.CreateDevice(param);
            return device;
        }
        catch (Exception ex)
        {
            var msg = $"新通讯创建设备失败 [{DeviceName}]：{ex.Message}";
            await NodeLog.ErrorAsync(msg);
            throw new InvalidOperationException(msg, ex);
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FillCommon。
    /// </summary>
    private void FillCommon(CommunicationParam param)
    {
        param.ProtocolType = Protocol;
        param.DeviceName = DeviceName;
        param.TimeoutMs = CommunicationTimeoutMs;
        param.RetryCount = CommunicationRetryCount;
        param.RetryDelayMs = CommunicationRetryDelayMs;
        param.AutoReconnect = AutoReconnect;
        param.ReconnectIntervalMs = ReconnectIntervalMs;
    }

    /// <summary>
    /// 执行移除操作；对应入口为 RemoveCandidateAsync。
    /// 正文中的关键协作调用包括 RemoveDeviceAsync、WarnAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task RemoveCandidateAsync(ICommunicationDevice device)
    {
        try
        {
            await Manager.RemoveDeviceAsync(device.DeviceId);
        }
        catch (Exception cleanupError)
        {
            await NodeLog.WarnAsync($"连接失败后清理临时设备异常 [{DeviceName}]：{cleanupError.Message}");
        }
    }
}
