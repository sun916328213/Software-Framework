using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Communication.Controls.Services;
using Communication.Controls.Views;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Socket;
using Microsoft.Win32;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Data;
using System.Windows.Threading;
using Extension.UI.Theme;
using Extension.UI.Controls;

namespace Communication.Controls.ViewModels;

/// <summary>
/// 通讯测试页视图模型：设备列表 + 连接控制 + 地址读写 + 原始收发 + 通讯日志。
/// 全部 IO 只经 <see cref="CommunicationSessionService"/>（其下就是 CommunicationNew 一套接口），
/// 输出只有通讯日志一处（接收数据页签已合并进日志的 RX 记录）。
/// </summary>
public sealed partial class CommunicationViewModel : ObservableObject
{
    private readonly CommunicationSessionService _session;
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;
    private readonly ICollectionView _logView;

    private CancellationTokenSource? _operationCts;
    private long _rxCount;
    private long _txCount;

    [ObservableProperty]
    private DeviceViewModel? selectedDevice;

    [ObservableProperty]
    private string address = "HR1";

    [ObservableProperty]
    private string readLength = "10";

    [ObservableProperty]
    private string payload = "00";

    [ObservableProperty]
    private bool autoScrollLog = true;

    [ObservableProperty]
    private bool useHex;

    [ObservableProperty]
    private string rxCountText = "0";

    [ObservableProperty]
    private string txCountText = "0";

    [ObservableProperty]
    private string deviceSummary = "设备 0 · 在线 0";

    [ObservableProperty]
    private string lastElapsed = "-";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusHint = "就绪";

    [ObservableProperty]
    private string logKeyword = string.Empty;

    [ObservableProperty]
    private string logLevelFilter = "全部";

    [ObservableProperty]
    private string logDeviceFilter = "全部";

    /// <summary>日志是否跟随左侧选中设备：选中即只看该设备的日志，效果等同“一个设备一个日志”。手动改设备下拉会自动取消跟随。</summary>
    [ObservableProperty]
    private bool followSelectedDevice = true;

    private bool _syncingDeviceFilter;

    /// <summary>日志按设备过滤的下拉选项：替代原来接收数据页的按设备隔离。</summary>
    public IReadOnlyList<string> LogDeviceOptions => ["全部", "Session", .. Devices.Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()];

    // ===== 发送便捷：循环发送 + 历史报文 =====

    [ObservableProperty]
    private bool loopSendEnabled;

    [ObservableProperty]
    private string loopIntervalMs = "1000";

    [ObservableProperty]
    private int loopSendFrames;

    /// <summary>已发送过的报文历史（最新在前），下拉复用。</summary>
    public ObservableCollection<string> PayloadHistory { get; } = [];

    public CommunicationViewModel(
        CommunicationSessionService session,
        ILogService log,
        IDialogService dialogs)
    {
        _session = session;
        _log = log;
        _dialogs = dialogs;

        _log.RxReceived += OnRxCounted;
        _log.TxReceived += OnTxCounted;

        _session.ReceiveDisplayFormat = UseHex ? "HEX" : "文本";

        Devices.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(LogDeviceOptions));
        };

        LogLevelOptions = ["全部", "INFO", "OK", "WARN", "FAIL", "RX", "TX"];
        _logView = CollectionViewSource.GetDefaultView(Logs);
        _logView.Filter = FilterLog;
        _log.Info("Session", $"CommunicationNew 会话就绪（主题引擎 { ThemeService.EngineTag}）");
    }

    public IReadOnlyList<string> LogLevelOptions { get; }

    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    public ObservableCollection<LogEntry> Logs => _log.Entries;

    /// <summary>地址提示：随选中设备的协议变化。</summary>
    public string AddressHint => SelectedDevice?.Info.AddressHint ?? "-";

    /// <summary>地址读写能力：IAddressReader + 协议目录双重判定，避免 TCP/UDP/串口伪装成功。</summary>
    public bool DeviceSupportsAddress => SelectedDevice is not null
        && ProtocolCatalog.SupportsAddress(SelectedDevice.Device);

    /// <summary>原始收发能力：仅字节流/数据报协议，HTTP/WebApi 这类高层 adapter 不走此路。</summary>
    public bool DeviceSupportsRaw => SelectedDevice is not null
        && ProtocolCatalog.SupportsRaw(SelectedDevice.Device.ProtocolType);

    /// <summary>请求回复能力：TCP 服务端只有单向发送，不能发一等一拍。</summary>
    public bool DeviceSupportsRequestReply => SelectedDevice?.Device is IRequestReplyClient;

    /// <summary>连接态：动作一律要求已连接，灰按钮只表示"没连接"这一种意思。</summary>
    public bool DeviceIsConnected => SelectedDevice is { IsConnected: true };

    /// <summary>收发区的禁用原因：说不清为什么点不动，就等于界面坏了。</summary>
    public string RawNotice => SelectedDevice is not { } device
        ? "未选择设备：先在左侧列表里选一台，或新建一台。"
        : !DeviceSupportsRaw && !DeviceSupportsAddress
            ? $"{device.Protocol}（{(device.Device.Role == DeviceRole.Server ? "服务端" : "客户端")}）不在这个页面上主动收发：它只在对端请求到来时按帧应答。"
            : !DeviceSupportsRaw
                ? $"{device.Protocol} 只能按地址读取 / 写入，不支持原始字节收发；如需发送任意字节，请新建 TCP Socket / UDP / 串口设备。"
                : DeviceIsConnected
                    ? string.Empty
                    : $"{device.Protocol} 支持原始收发，但设备还没连接：点左侧「连接」。";

    /// <summary>收发区底部说明：能干活时讲格式与广播，不能干活时讲为什么不能干。</summary>
    public string BottomNotice => RawNotice.Length > 0
        ? RawNotice
        : "内容格式跟随右上角 HEX 开关；对端主动推来的数据按时间追加到下方「通讯日志」（RX 记录，显示全文）。服务端设备的发送是广播，当前没有客户端连着时不会送达任何对端。";

    /// <summary>操作区标题：按协议切换，地址协议只谈读写、字节流协议只谈收发。</summary>
    public string OperationTitle => SelectedDevice is not { } device
        ? "读写与收发"
        : DeviceSupportsAddress && !DeviceSupportsRaw
            ? $"{device.Protocol} · 地址读写"
            : !DeviceSupportsAddress && DeviceSupportsRaw
                ? $"{device.Protocol} · 原始收发"
                : $"{device.Protocol} · 读写与收发";

    /// <summary>协议选型提示：直接取协议目录的 UsageHint，无设备时给一句引导。</summary>
    public string ProtocolUsageHint => SelectedDevice?.Info.UsageHint
        ?? "在左侧选择或新建一台设备，右侧会按它的协议自动切换操作区。";

    /// <summary>地址格式明细（如 Modbus 一基说明），地址组内联显示，无地址能力时为空。</summary>
    public string AddressDetailHint => DeviceSupportsAddress
        ? SelectedDevice!.Info.AddressRegexHint
        : string.Empty;

    /// <summary>
    /// 写入长度说明：地址协议没有"写入长度"这一栏，写多长完全由数据字节数决定。
    /// 不说这句，用户会一直找一个不存在的长度输入框。
    /// </summary>
    public string WriteLengthHint
    {
        get
        {
            if (!DeviceSupportsAddress)
            {
                return string.Empty;
            }

            if (UseHex && !TryParseHex(Payload, out _))
            {
                return "写入长度：HEX 还没填完整（每字节两位十六进制）";
            }

            var bytes = PayloadByteCount;
            return $"写入长度由数据决定：当前 {bytes} 字节"
                + (SelectedDevice!.Device.ProtocolType is ProtocolType.ModbusTcp or ProtocolType.ModbusRtu
                    ? $"（Modbus 每 2 字节 1 个寄存器，奇数长度会按寄存器对齐）"
                    : string.Empty);
        }
    }

    private int PayloadByteCount => UseHex
        ? Payload.Count(c => !char.IsWhiteSpace(c)) / 2
        : Encoding.UTF8.GetByteCount(Payload);

    partial void OnPayloadChanged(string value) => OnPropertyChanged(nameof(WriteLengthHint));

    /// <summary>能力摘要：灰掉的按钮要说清为什么。</summary>
    public string CapabilitySummary
    {
        get
        {
            if (SelectedDevice is not { } device)
            {
                return "未选择设备";
            }

            var caps = new List<string> { device.IsConnected ? "已连接" : "未连接" };
            if (DeviceSupportsAddress)
            {
                caps.Add("地址读写");
            }

            if (DeviceSupportsRaw)
            {
                caps.Add(device.Device is IRequestReplyClient ? "原始收发 / 请求回复" : "原始发送（服务端）");
            }

            caps.Add(device.Device is IReceiveSource<ReceivedPacket> ? "被动接收" : "事件接收");
            return string.Join(" · ", caps);
        }
    }

    private bool HasDevice => SelectedDevice is not null;

    /// <summary>列表里有没有设备：空列表时给一句引导，而不是留一块空白。</summary>
    public bool HasDevices => Devices.Count > 0;

    private bool CanConnect => SelectedDevice is { IsConnected: false };

    private bool CanDisconnect => SelectedDevice is { IsConnected: true };

    private bool CanReadOrWrite => DeviceSupportsAddress && DeviceIsConnected;

    private bool CanSendRaw => DeviceSupportsRaw && DeviceIsConnected && SelectedDevice!.Device is IRawSender;

    private bool CanSendAndReceive => CanSendRaw && SelectedDevice!.Device is IRequestReplyClient;

    /// <summary>可循环的动作：字节流协议循环「发送」，地址协议循环「读取」。</summary>
    public string LoopActionName => DeviceSupportsRaw ? "发送" : DeviceSupportsAddress ? "读取" : string.Empty;

    /// <summary>循环组标题：直接写明循环的是哪个动作，避免"循环发送"在地址协议下词不达意。</summary>
    public string LoopTitle => $"循环{LoopActionName}";

    /// <summary>循环发送可用：只要能收发就给它一个入口，否则用户会以为这个功能没做。</summary>
    public bool SupportsLoop => DeviceSupportsRaw || DeviceSupportsAddress;

    /// <summary>循环开始可用态：需设备支持收发、已连接，且当前没有运行中的循环。</summary>
    private bool CanStartLoop => SupportsLoop && DeviceIsConnected && !LoopSendEnabled;

    /// <summary>循环运行状态：不说这句，用户分不清"没点动"和"已经在循环"。</summary>
    public string LoopSendStateText => LoopSendEnabled
        ? $"循环中：每 {LoopIntervalMs} ms {LoopActionName}一次，已执行 {LoopSendFrames} 次"
        : string.Empty;

    partial void OnLoopSendEnabledChanged(bool value)
    {
        StartLoopSendCommand.NotifyCanExecuteChanged();
        StopLoopSendCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(LoopSendStateText));
    }

    partial void OnLoopSendFramesChanged(int value) => OnPropertyChanged(nameof(LoopSendStateText));

    partial void OnLoopIntervalMsChanged(string value) => OnPropertyChanged(nameof(LoopSendStateText));

    private bool CanBatch => Devices.Count > 0 && !IsBusy;

    partial void OnSelectedDeviceChanged(DeviceViewModel? value)
    {
        // 换设备后继续对旧设备发帧是没人预期的行为，先停掉上一台留下的循环。
        CancelLoopSend("切换设备");

        // 跟随模式下切设备即切日志：一个设备对应一份日志视图。
        if (FollowSelectedDevice)
        {
            _syncingDeviceFilter = true;
            try
            {
                LogDeviceFilter = value?.Name ?? "全部";
            }
            finally
            {
                _syncingDeviceFilter = false;
            }
        }

        OnPropertyChanged(nameof(AddressHint));
        OnPropertyChanged(nameof(DeviceSupportsAddress));
        OnPropertyChanged(nameof(DeviceSupportsRaw));
        OnPropertyChanged(nameof(DeviceSupportsRequestReply));
        OnPropertyChanged(nameof(DeviceIsConnected));
        OnPropertyChanged(nameof(CapabilitySummary));
        OnPropertyChanged(nameof(OperationTitle));
        OnPropertyChanged(nameof(ProtocolUsageHint));
        OnPropertyChanged(nameof(AddressDetailHint));
        OnPropertyChanged(nameof(WriteLengthHint));
        OnPropertyChanged(nameof(LoopActionName));
        OnPropertyChanged(nameof(LoopTitle));
        OnPropertyChanged(nameof(SupportsLoop));
        OnPropertyChanged(nameof(RawNotice));
        OnPropertyChanged(nameof(BottomNotice));
        ApplyProtocolAddressHint();
        RaiseCommandStates();
    }

    partial void OnUseHexChanged(bool value)
    {
        _session.ReceiveDisplayFormat = value ? "HEX" : "文本";
        OnPropertyChanged(nameof(WriteLengthHint));
    }

    partial void OnLogLevelFilterChanged(string value) => _logView.Refresh();

    partial void OnLogKeywordChanged(string value) => _logView.Refresh();

    partial void OnLogDeviceFilterChanged(string value)
    {
        // 手动改设备下拉视为退出跟随；程序自动跟随选中设备时不退出。
        if (!_syncingDeviceFilter && FollowSelectedDevice
            && !string.Equals(value, SelectedDevice?.Name, StringComparison.Ordinal))
        {
            FollowSelectedDevice = false;
        }

        _logView.Refresh();
    }

    partial void OnFollowSelectedDeviceChanged(bool value)
    {
        // 重新勾选跟随时立刻对齐到当前选中设备。
        if (value && SelectedDevice is { } device)
        {
            _syncingDeviceFilter = true;
            try
            {
                LogDeviceFilter = device.Name;
            }
            finally
            {
                _syncingDeviceFilter = false;
            }
        }
    }

    // ===== 命令 =====

    [RelayCommand]
    private void AddDevice()
    {
        _dialogs.ShowDialog(nameof(DeviceConfigDialog), new DialogParameters(), result =>
        {
            if (result.Result != ButtonResult.OK
                || !result.Parameters.TryGetValue<CommunicationParam>("param", out var param))
            {
                return;
            }

            try
            {
                AddDevice(_session.CreateDevice(param));
            }
            catch (Exception ex)
            {
                _log.Error("Session", $"创建设备失败: {ex.Message}");
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private Task ConnectAsync() => WithDeviceAsync(device =>
        RunDeviceOperationAsync(device, ct => _session.ConnectDeviceAsync(device.Device, ct)));

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private Task DisconnectAsync() => WithDeviceAsync(device =>
    {
        CancelLoopSend("断开连接");
        return RunDeviceOperationAsync(device, ct => _session.DisconnectDeviceAsync(device.Device, ct));
    });

    [RelayCommand(CanExecute = nameof(HasDevice))]
    private async Task RemoveAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        CancelLoopSend("设备移除");
        var index = Devices.IndexOf(device);
        await RunDeviceOperationAsync(device, _ => _session.RemoveDeviceAsync(device.Id));
        Devices.Remove(device);
        SelectedDevice = Devices.Count > 0
            ? Devices[Math.Clamp(index, 0, Devices.Count - 1)]
            : null;
        UpdateSummary();
        RaiseCommandStates();
    }

    /// <summary>
    /// 编辑选中设备参数：快照当前参数进对话框（深拷贝），确认后先移除旧设备再按编辑后参数重建，
    /// DeviceId 保持不变，列表位置与选中态也保持。取消则快照直接丢弃，在线设备不受影响。
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDevice))]
    private void EditDevice()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        var snapshot = _session.GetParam(device.Id);
        if (snapshot is null)
        {
            _log.Error("Session", $"读取 [{device.Name}] 参数失败，无法编辑");
            return;
        }

        _dialogs.ShowDialog(nameof(DeviceConfigDialog), new DialogParameters { { "param", snapshot } }, result =>
        {
            if (result.Result != ButtonResult.OK
                || !result.Parameters.TryGetValue<CommunicationParam>("param", out var edited)
                || edited is null)
            {
                return;
            }

            _ = ReplaceDeviceAsync(device, edited);
        });
    }

    private async Task ReplaceDeviceAsync(DeviceViewModel oldDevice, CommunicationParam edited)
    {
        var index = Math.Clamp(Devices.IndexOf(oldDevice), 0, Math.Max(0, Devices.Count - 1));
        if (string.IsNullOrWhiteSpace(edited.DeviceId))
        {
            edited.DeviceId = oldDevice.Id;
        }

        try
        {
            await RunDeviceOperationAsync(oldDevice, _ => _session.RemoveDeviceAsync(oldDevice.Id));
            Devices.Remove(oldDevice);
            var created = _session.CreateDevice(edited);
            var vm = TrackDevice(created);
            Devices.Insert(Math.Min(index, Devices.Count), vm);
            SelectedDevice = vm;
            UpdateSummary();
            RaiseCommandStates();
            _log.Success("Session", $"[{vm.Name}] 参数已更新");
        }
        catch (Exception ex)
        {
            _log.Error("Session", $"更新设备失败: {ex.Message}");
            UpdateSummary();
            RaiseCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanReadOrWrite))]
    private Task ReadAsync() => WithDeviceAsync(ReadSelectedAsync);

    [RelayCommand(CanExecute = nameof(CanReadOrWrite))]
    private Task WriteAsync() => WithDeviceAsync(WriteSelectedAsync);

    [RelayCommand(CanExecute = nameof(CanSendRaw))]
    private Task SendRawAsync() => WithDeviceAsync(SendRawSelectedAsync);

    [RelayCommand(CanExecute = nameof(CanSendAndReceive))]
    private Task SendAndReceiveAsync() => WithDeviceAsync(SendAndReceiveSelectedAsync);

    [RelayCommand(CanExecute = nameof(CanBatch))]
    private Task ConnectAllAsync() => RunBatchOperationAsync("全部连接", ct => _session.ConnectAllAsync(ct));

    [RelayCommand(CanExecute = nameof(CanBatch))]
    private Task DisconnectAllAsync() => RunBatchOperationAsync("全部断开", ct => _session.DisconnectAllAsync(ct));

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void CancelOperation()
    {
        // 只取消当前这一次，循环本身要一起停：否则"取消"完下一帧又到，等于没取消。
        CancelLoopSend("取消操作");
        _operationCts?.Cancel();
    }

    // ===== 循环发送 =====

    private CancellationTokenSource? _loopSendCts;

    [RelayCommand(CanExecute = nameof(CanStartLoop))]
    private async Task StartLoopSendAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        // 按能力挑循环动作：字节流协议循环发送原始字节，地址协议循环按地址读取。
        Func<DeviceViewModel, Task> tick = DeviceSupportsRaw
            ? SendRawSelectedAsync
            : ReadSelectedAsync;

        if (!int.TryParse(LoopIntervalMs.Trim(), out var interval) || interval < 10)
        {
            interval = 1000;
            LoopIntervalMs = "1000";
            _log.Warning("Session", "循环间隔要填 10 以上的毫秒数，本次按 1000 ms 执行");
        }

        LoopSendEnabled = true;
        LoopSendFrames = 0;
        _loopSendCts = new CancellationTokenSource();
        var ct = _loopSendCts.Token;
        // 循环必须每次都由 UI 线程发起动作：CommunicationNew 内部一路 ConfigureAwait(false)，
        // 换到线程池上续接就会把 IsBusy / 接收窗口这些绑定属性写到非 UI 线程。
        var dispatcher = Dispatcher.CurrentDispatcher;
        _log.Info("Session", $"[{device.Name}] 循环{LoopActionName}已启动：每 {interval} ms 一次");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // 复用单次动作：日志、TX/RX 计数、失败原因都在里面处理，循环不重算一遍。
                var running = dispatcher.InvokeAsync(new Func<Task>(async () =>
                {
                    await tick(device);
                    LoopSendFrames++;
                }));
                await running.Task;
                await running.Result;
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                await Task.Delay(interval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 用户手动停止或设备离线
        }
        catch (Exception ex)
        {
            _log.Error(device.Name, $"循环{LoopActionName}异常: {ex.Message}");
        }
        finally
        {
            DispatcherEx.UIInvokeSafe(() =>
            {
                _loopSendCts?.Dispose();
                _loopSendCts = null;
                LoopSendEnabled = false;
            });
        }
    }

    [RelayCommand(CanExecute = nameof(LoopSendEnabled))]
    private void StopLoopSend() => CancelLoopSend("手动停止");

    /// <summary>停止循环；设备断开 / 移除 / 切换时都走这里，不能留一个后台任务继续对旧设备发帧。</summary>
    private void CancelLoopSend(string reason)
    {
        if (_loopSendCts is not { } cts)
        {
            return;
        }

        _log.Info("Session", $"循环发送已停止（{reason}）");
        cts.Cancel();
    }

    // ===== 历史报文 =====

    private void PushHistory(string payload)
    {
        var trimmed = payload.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        // 去重后插到最前，最多保留 20 条
        PayloadHistory.Remove(trimmed);
        PayloadHistory.Insert(0, trimmed);
        while (PayloadHistory.Count > 20)
        {
            PayloadHistory.RemoveAt(PayloadHistory.Count - 1);
        }
    }


    [RelayCommand]
    private void ClearLog() => _log.Clear();

    [RelayCommand]
    private void ResetCounters()
    {
        _rxCount = 0;
        _txCount = 0;
        RxCountText = "0";
        TxCountText = "0";
    }

    [RelayCommand]
    private void ExportLog()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "文本文件 (*.txt)|*.txt|CSV (*.csv)|*.csv",
            FileName = $"comm-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var lines = Logs.Select(e => $"{e.TimeText}\t{e.LevelText.Trim()}\t{e.Source}\t{e.Message}");
            File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
            _log.Success("Session", $"日志已导出 {Logs.Count} 条 → {dialog.FileName}");
        }
        catch (Exception ex)
        {
            _log.Error("Session", $"导出日志失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveConfig()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = $"comm-devices-{DateTime.Now:yyyyMMdd-HHmmss}.json",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            DeviceConfigStore.Save(dialog.FileName, _session.GetAllParams());
            _log.Success("Session", $"配置已保存 {Devices.Count} 台 → {dialog.FileName}");
        }
        catch (Exception ex)
        {
            _log.Error("Session", $"保存配置失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadConfig()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        List<CommunicationParam> parameters;
        int skipped;
        try
        {
            (parameters, skipped) = DeviceConfigStore.Load(dialog.FileName);
        }
        catch (Exception ex)
        {
            _log.Error("Session", $"加载配置失败: {ex.Message}");
            return;
        }

        var restored = 0;
        foreach (var param in parameters)
        {
            try
            {
                AddDevice(_session.RestoreDevice(param));
                restored++;
            }
            catch (Exception ex)
            {
                _log.Error("Session", $"恢复 [{param.DeviceName}] 失败: {ex.Message}");
                skipped++;
            }
        }

        UpdateSummary();
        RaiseCommandStates();
        _log.Success("Session", $"配置恢复 {restored} 台，跳过 {skipped} 条");
    }

    // ===== 操作实现 =====

    private void AddDevice(ICommunicationDevice device)
    {
        var vm = TrackDevice(device);
        Devices.Add(vm);
        SelectedDevice = vm;
        UpdateSummary();
        RaiseCommandStates();
    }

    /// <summary>包一层 DeviceViewModel 并挂好连接态监听（新增/编辑替换共用）。</summary>
    private DeviceViewModel TrackDevice(ICommunicationDevice device)
    {
        var vm = new DeviceViewModel(device);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceViewModel.IsConnected))
            {
                if (!vm.IsConnected && ReferenceEquals(vm, SelectedDevice))
                {
                    CancelLoopSend("设备已离线");
                }

                // 设备事件多来自 IO 线程，命令可用态与绑定属性都要回 UI 线程刷。
                 DispatcherEx.UIInvokeSafe(() =>
                {
                    UpdateSummary();
                    OnPropertyChanged(nameof(DeviceIsConnected));
                    OnPropertyChanged(nameof(CapabilitySummary));
                    OnPropertyChanged(nameof(RawNotice));
                    OnPropertyChanged(nameof(BottomNotice));
                    RaiseCommandStates();
                });
            }
        };
        return vm;
    }

    private Task WithDeviceAsync(Func<DeviceViewModel, Task> operation) =>
        SelectedDevice is { } device ? operation(device) : Task.CompletedTask;

    private Task ReadSelectedAsync(DeviceViewModel device)
    {
        if (!TryParseLength(ReadLength, out var length) || device.Device is not IAddressReader reader)
        {
            return Task.CompletedTask;
        }

        var address = Address.Trim();
        return RunDeviceOperationAsync(device, async ct =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var data = await reader.ReadAsync(address, length, ct);
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            _log.Success(device.Name, $"读取 {address}×{length} → {data.Length} 字节, {sw.ElapsedMilliseconds} ms");
            _log.Rx(device.Name, data.Length == 0 ? "(空响应)" : ToDisplay(data));
        });
    }

    private Task WriteSelectedAsync(DeviceViewModel device)
    {
        if (device.Device is not IAddressWriter writer)
        {
            return Task.CompletedTask;
        }

        var address = Address.Trim();
        if (!TryParsePayload(Payload, out var data))
        {
            _log.Warning(device.Name, "写入数据格式无效（HEX 勾选时按十六进制，否则按 UTF-8 文本）");
            return Task.CompletedTask;
        }

        return RunDeviceOperationAsync(device, async ct =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ok = await writer.WriteAsync(address, data, ct);
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            _log.Tx(device.Name, ToDisplay(data));
            _log.Success(device.Name, $"写入 {address} → {(ok ? "成功" : "失败")}, {sw.ElapsedMilliseconds} ms");
        });
    }

    private Task SendRawSelectedAsync(DeviceViewModel device)
    {
        if (device.Device is not IRawSender sender || !TryParsePayload(Payload, out var payload))
        {
            _log.Warning(device.Name, "发送内容格式无效（HEX 勾选时按十六进制，否则按 UTF-8 文本）");
            return Task.CompletedTask;
        }

        PushHistory(Payload);

        return RunDeviceOperationAsync(device, async ct =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ok = await sender.SendAsync(payload, ct);
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            _log.Tx(device.Name, ToDisplay(payload));
            _log.Success(device.Name, ok ? $"发送成功, {sw.ElapsedMilliseconds} ms" : "发送失败");
            if (sender is TcpServerDevice { ConnectedClients.Count: 0 })
            {
                _log.Warning(device.Name, "服务端当前没有已连接的客户端，本次发送没有送达任何对端");
            }
        });
    }

    private Task SendAndReceiveSelectedAsync(DeviceViewModel device)
    {
        if (device.Device is not IRequestReplyClient client || !TryParsePayload(Payload, out var payload))
        {
            _log.Warning(device.Name, "发送内容格式无效（HEX 勾选时按十六进制，否则按 UTF-8 文本）");
            return Task.CompletedTask;
        }

        PushHistory(Payload);

        return RunDeviceOperationAsync(device, async ct =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.SendAndReceiveAsync(payload, ct);
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";

            _log.Tx(device.Name, ToDisplay(payload));
            _log.Rx(device.Name, $"{(response.Length == 0 ? "(空响应)" : ToDisplay(response))} ({sw.ElapsedMilliseconds} ms)");
        });
    }

    /// <summary>单设备操作：可取消，耗时计入 <see cref="LastElapsed"/>，取消与失败分别记日志。</summary>
    private async Task RunDeviceOperationAsync(DeviceViewModel device, Func<CancellationToken, Task> operation)
    {
        device.IsBusy = true;
        IsBusy = true;
        StatusHint = $"{device.Name} 执行中…（可取消）";
        _operationCts = new CancellationTokenSource();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await operation(_operationCts.Token);
            StatusHint = "就绪";
        }
        catch (OperationCanceledException)
        {
            _log.Warning(device.Name, "操作已取消");
            StatusHint = "已取消";
        }
        catch (Exception ex)
        {
            _log.Error(device.Name, ex.Message);
            StatusHint = $"失败: {ex.Message}";
        }
        finally
        {
            sw.Stop();
            device.IsBusy = false;
            _operationCts?.Dispose();
            _operationCts = null;
            IsBusy = false;
            UpdateSummary();
            RaiseCommandStates();
        }
    }

    private async Task RunBatchOperationAsync(string name, Func<CancellationToken, Task> operation)
    {
        IsBusy = true;
        StatusHint = $"{name}执行中…（可取消）";
        _operationCts = new CancellationTokenSource();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await operation(_operationCts.Token);
            _log.Success("Session", $"{name}完成, {sw.ElapsedMilliseconds} ms");
            StatusHint = $"{name}完成";
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Session", $"{name}已取消");
            StatusHint = $"{name}已取消";
        }
        catch (Exception ex)
        {
            _log.Error("Session", $"{name}失败: {ex.Message}");
            StatusHint = $"{name}失败";
        }
        finally
        {
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            _operationCts?.Dispose();
            _operationCts = null;
            IsBusy = false;
            UpdateSummary();
            RaiseCommandStates();
        }
    }

    private void UpdateSummary()
    {
        var online = Devices.Count(d => d.IsConnected);
        DeviceSummary = $"设备 {Devices.Count} · 在线 {online}";
        OnPropertyChanged(nameof(LogDeviceOptions));
        if (LogDeviceFilter != "全部" && !LogDeviceOptions.Contains(LogDeviceFilter))
        {
            LogDeviceFilter = "全部";
        }
    }

    /// <summary>CommunityToolkit 命令不挂 CommandManager，能力位变化后要手工刷新可用态。</summary>
    private void RaiseCommandStates()
    {
        EditDeviceCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        ReadCommand.NotifyCanExecuteChanged();
        WriteCommand.NotifyCanExecuteChanged();
        SendRawCommand.NotifyCanExecuteChanged();
        SendAndReceiveCommand.NotifyCanExecuteChanged();
        StartLoopSendCommand.NotifyCanExecuteChanged();
        StopLoopSendCommand.NotifyCanExecuteChanged();
        ConnectAllCommand.NotifyCanExecuteChanged();
        DisconnectAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 按协议填一个能直接点的样例地址。
    /// CommunicationNew 的 Modbus 地址是一基（HR0 会被 ModbusAddressValue 判成非法地址），所以样例给 HR1。
    /// </summary>
    private void ApplyProtocolAddressHint()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        Address = device.ProtocolType switch
        {
            ProtocolType.ModbusTcp or ProtocolType.ModbusRtu => "HR1",
            ProtocolType.MitsubishiMc => "D100",
            ProtocolType.OpcUa => "ns=2;s=Tag1",
            ProtocolType.Http => "/api/data",
            ProtocolType.WebApi => "/api/v1/data",
            _ => Address,
        };
    }

    private bool FilterLog(object item)
    {
        if (item is not LogEntry entry)
        {
            return false;
        }

        if (LogLevelFilter != "全部" && entry.LevelText.Trim() != LogLevelFilter)
        {
            return false;
        }

        if (LogDeviceFilter != "全部" && !string.Equals(entry.Source, LogDeviceFilter, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(LogKeyword))
        {
            var keyword = LogKeyword.Trim();
            if (!entry.Source.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                && !entry.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryParsePayload(string input, out byte[] payload)
    {
        if (UseHex)
        {
            var ok = TryParseHex(input, out payload);
            if (!ok)
            {
                StatusHint = "内容不是合法 HEX：每字节两位十六进制，或取消 HEX 勾选按文本发送";
            }

            return ok;
        }

        payload = Encoding.UTF8.GetBytes(input);
        return true;
    }

    private void OnRxCounted(string source, string message) =>
        DispatcherEx.UIInvokeSafe(() =>
        {
            _rxCount++;
            RxCountText = _rxCount.ToString("N0");
        });

    private void OnTxCounted(string source, string message) =>
       DispatcherEx.UIInvokeSafe(() =>
        {
            _txCount++;
            TxCountText = _txCount.ToString("N0");
        });

    private static bool TryParseHex(string input, out byte[] data)
    {
        data = [];
        var text = new string([.. input.Where(c => !char.IsWhiteSpace(c))]);
        if (text.Length is 0 or 1 || text.Length % 2 != 0)
        {
            return false;
        }

        try
        {
            data = Convert.FromHexString(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseLength(string input, out int length) =>
        int.TryParse(input.Trim(), out length) && length > 0;

    private static string ToHex(byte[] data) =>
        data.Length == 0 ? string.Empty : BitConverter.ToString(data).Replace("-", " ");

    /// <summary>收发统一显示：跟随 HEX 开关。</summary>
    private string ToDisplay(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        return UseHex ? ToHex(data) : Encoding.UTF8.GetString(data);
    }
}
