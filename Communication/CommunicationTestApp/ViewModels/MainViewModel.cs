using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CommunicationNew.Core;
using CommunicationTestApp.Services;
using CommunicationTestApp.Views;
using Microsoft.Win32;

namespace CommunicationTestApp.ViewModels;

/// <summary>
/// 主窗口视图模型 —— 通讯测试程序的核心。
/// 左侧设备列表，右侧操作面板（连接控制 / 读写测试 / 原始收发 / 日志）。
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly CommunicationSessionService _session;
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;
    private readonly Services.ThemeService _theme;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    private DeviceViewModel? _selectedDevice;
    private string _readAddress = "HR0";
    private string _readLength = "10";
    private string _readResult = string.Empty;
    private string _writeAddress = "HR0";
    private string _writeData = "00 01";
    private string _rawInput = "01 03 00 00 00 0A";
    private string _rawResult = string.Empty;
    private bool _autoScrollLog = true;
    private bool _useHex = true;
    private byte[] _lastResponse = [];
    private string _rxCountText = "0";
    private string _txCountText = "0";
    private long _rxCount;
    private long _txCount;
    private string _deviceSummary = "设备 0 · 在线 0";
    private string _lastElapsed = "-";
    private bool _isBusy;
    private CancellationTokenSource? _operationCts;
    private string _logKeyword = string.Empty;
    private string _logLevelFilter = "全部";
    private string _statusHint = "就绪";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        CommunicationSessionService session,
        ILogService log,
        IDialogService dialogs,
        Services.ThemeService theme)
    {
        _session = session;
        _log = log;
        _dialogs = dialogs;
        _theme = theme;

        CommunicationSessionService.ReceiveDisplayFormat = _useHex ? "HEX" : "文本";
        _log.RxReceived += OnRxCounted;
        _log.TxReceived += OnTxCounted;

        LogLevelOptions = ["全部", "INFO", "OK", "WARN", "FAIL", "RX", "TX"];
        var view = CollectionViewSource.GetDefaultView(Logs);
        view.Filter = FilterLog;
    }

    public IReadOnlyList<string> LogLevelOptions { get; }

    // ===== 绑定属性 =====

    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    public ObservableCollection<LogEntry> Logs => _log.Entries;

    public DeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetField(ref _selectedDevice, value))
            {
                UpdateOperationHints();
            }
        }
    }

    public string ReadAddress
    {
        get => _readAddress;
        set => SetField(ref _readAddress, value);
    }

    public string ReadLength
    {
        get => _readLength;
        set => SetField(ref _readLength, value);
    }

    public string ReadResult
    {
        get => _readResult;
        set => SetField(ref _readResult, value);
    }

    public string WriteAddress
    {
        get => _writeAddress;
        set => SetField(ref _writeAddress, value);
    }

    public string WriteData
    {
        get => _writeData;
        set => SetField(ref _writeData, value);
    }

    public string RawInput
    {
        get => _rawInput;
        set => SetField(ref _rawInput, value);
    }

    public string RawResult
    {
        get => _rawResult;
        set => SetField(ref _rawResult, value);
    }

    public bool AutoScrollLog
    {
        get => _autoScrollLog;
        set => SetField(ref _autoScrollLog, value);
    }

    /// <summary>收发统一格式：勾选按 HEX 解析/显示，去掉按 UTF-8 文本收发。</summary>
    public bool UseHex
    {
        get => _useHex;
        set
        {
            if (SetField(ref _useHex, value))
            {
                CommunicationSessionService.ReceiveDisplayFormat = value ? "HEX" : "文本";
                RawResult = ToDisplayRx(_lastResponse);
            }
        }
    }

    public string RxCountText
    {
        get => _rxCountText;
        private set => SetField(ref _rxCountText, value);
    }

    public string TxCountText
    {
        get => _txCountText;
        private set => SetField(ref _txCountText, value);
    }

    public string AddressHint => SelectedDevice?.Info.AddressHint ?? "-";

    /// <summary>设备总数/在线数汇总（状态栏与标题区共用）。</summary>
    public string DeviceSummary
    {
        get => _deviceSummary;
        private set => SetField(ref _deviceSummary, value);
    }

    /// <summary>最近一次读写/收发耗时。</summary>
    public string LastElapsed
    {
        get => _lastElapsed;
        private set => SetField(ref _lastElapsed, value);
    }

    /// <summary>是否有耗时操作正在执行（批量/读写共用，驱动取消按钮与忙指示）。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    /// <summary>状态提示（就绪/执行中/已取消/失败原因）。</summary>
    public string StatusHint
    {
        get => _statusHint;
        private set => SetField(ref _statusHint, value);
    }

    /// <summary>日志级别过滤（全部/INFO/OK/WARN/FAIL/RX/TX）。</summary>
    public string LogLevelFilter
    {
        get => _logLevelFilter;
        set
        {
            if (SetField(ref _logLevelFilter, value))
            {
                RefreshLogFilter();
            }
        }
    }

    /// <summary>日志关键字过滤（匹配来源与内容，不区分大小写）。</summary>
    public string LogKeyword
    {
        get => _logKeyword;
        set
        {
            if (SetField(ref _logKeyword, value))
            {
                RefreshLogFilter();
            }
        }
    }

    public bool HasDevice => SelectedDevice is not null;

    /// <summary>地址读写能力：IAddressReader + 协议目录双重判定，避免 TCP/UDP/Serial 伪装成功。</summary>
    public bool DeviceSupportsAddress => SelectedDevice is not null
        && ProtocolCatalog.SupportsAddress(SelectedDevice.Device);

    /// <summary>原始收发能力：仅 TCP/UDP/Serial 字节流/数据报；HTTP/WebApi 高层 adapter 不走此 seam。</summary>
    public bool DeviceSupportsRaw => SelectedDevice is not null
        && ProtocolCatalog.SupportsRaw(SelectedDevice.Device.ProtocolType);

    // ===== 命令 =====

    public ICommand AddDeviceCommand => new RelayCommand(_ => AddDevice());

    public ICommand ConnectCommand => new RelayCommand(
        async _ => await ConnectSelectedAsync(),
        _ => SelectedDevice is { IsConnected: false });

    public ICommand DisconnectCommand => new RelayCommand(
        async _ => await DisconnectSelectedAsync(),
        _ => SelectedDevice is { IsConnected: true });

    public ICommand RemoveDeviceCommand => new RelayCommand(
        async _ => await RemoveSelectedAsync(),
        _ => SelectedDevice is not null);

    public ICommand ReadCommand => new RelayCommand(
        async _ => await ReadSelectedAsync(),
        _ => DeviceSupportsAddress);

    public ICommand WriteCommand => new RelayCommand(
        async _ => await WriteSelectedAsync(),
        _ => DeviceSupportsAddress);

    public ICommand SendRawCommand => new RelayCommand(
        async _ => await SendRawSelectedAsync(),
        _ => DeviceSupportsRaw);

    public ICommand SendAndReceiveCommand => new RelayCommand(
        async _ => await SendAndReceiveSelectedAsync(),
        _ => DeviceSupportsRaw);

    public ICommand ConnectAllCommand => new RelayCommand(
        async _ => await ConnectAllAsync(),
        _ => Devices.Count > 0 && !IsBusy);

    public ICommand DisconnectAllCommand => new RelayCommand(
        async _ => await DisconnectAllAsync(),
        _ => Devices.Count > 0 && !IsBusy);

    public ICommand CancelOperationCommand => new RelayCommand(
        _ => _operationCts?.Cancel(),
        _ => IsBusy);

    public ICommand ExportLogCommand => new RelayCommand(_ => ExportLog());

    public ICommand SaveConfigCommand => new RelayCommand(_ => SaveConfig());

    public ICommand LoadConfigCommand => new RelayCommand(async _ => await LoadConfigAsync());

    public ICommand ClearLogCommand => new RelayCommand(_ => _log.Clear());

    public ICommand ResetCountersCommand => new RelayCommand(() =>
    {
        _rxCount = 0;
        _txCount = 0;
        RxCountText = "0";
        TxCountText = "0";
    });

    /// <summary>黑白主题切换（切换后写日志 + 状态栏反馈，点下去一定看得见）。</summary>
    public ICommand ToggleThemeCommand => new RelayCommand(() =>
    {
        _theme.Toggle();
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeIcon));
        var name = _theme.IsDarkTheme ? "深色" : "浅色";
        StatusHint = $"{name}主题";
        if (_theme.LastError is null)
        {
            _log.Success("Session", $"已切换{name}主题");
        }
        else
        {
            _log.Warning("Session", $"已切换{name}主题，但{_theme.LastError}");
        }
    });

    /// <summary>当前是否深色主题（控制切换按钮图标）。</summary>
    public bool IsDarkTheme => _theme.IsDarkTheme;

    /// <summary>切换按钮图标：当前深色 → 显示太阳（点击转浅色）。</summary>
    public string ThemeIcon => _theme.IsDarkTheme ? "\uE706" : "\uE708";

    // ===== 操作实现 =====

    private void AddDevice()
    {
        _dialogs.ShowDialog(nameof(DeviceConfigDialog), new DialogParameters(),
            new DialogCallback().OnClose(r =>
            {
                if (r.Result != ButtonResult.OK || !r.Parameters.TryGetValue<CommunicationParam>("param", out var param))
                {
                    return;
                }

                try
                {
                    var device = _session.CreateDevice(param);
                    var vm = new DeviceViewModel(device);
                    vm.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(DeviceViewModel.IsConnected))
                        {
                            UpdateSummary();
                        }
                    };
                    Devices.Add(vm);
                    SelectedDevice = vm;
                    UpdateSummary();
                }
                catch (Exception ex)
                {
                    _log.Error("Session", $"创建设备失败: {ex.Message}");
                }
            }));
    }

    private async Task ConnectAllAsync()
    {
        if (Devices.Count == 0)
        {
            return;
        }
        await RunBatchOperation("全部连接", ct => _session.ConnectAllAsync(ct));
    }

    private async Task DisconnectAllAsync()
    {
        if (Devices.Count == 0)
        {
            return;
        }
        await RunBatchOperation("全部断开", ct => _session.DisconnectAllAsync(ct));
    }

    private async Task RunBatchOperation(string name, Func<CancellationToken, Task> operation)
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
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task ConnectSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        await RunDeviceOperation(device, ct => _session.ConnectDeviceAsync(device.Device, ct));
    }

    private async Task DisconnectSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        await RunDeviceOperation(device, ct => _session.DisconnectDeviceAsync(device.Device, ct));
    }

    private async Task RemoveSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        var index = Devices.IndexOf(device);
        await RunDeviceOperation(device, async ct => { await _session.RemoveDeviceAsync(device.Id); });
        Devices.Remove(device);
        SelectedDevice = Devices.Count > 0
            ? Devices[Math.Clamp(index, 0, Devices.Count - 1)]
            : null;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var online = Devices.Count(d => d.IsConnected);
        DeviceSummary = $"设备 {Devices.Count} · 在线 {online}";
    }

    private async Task ReadSelectedAsync()
    {
        if (SelectedDevice is not { } device || !TryParseLength(ReadLength, out var length))
        {
            return;
        }

        var address = ReadAddress.Trim();
        await RunDeviceOperation(device, async ct =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var data = await device.Device.ReadAsync(address, length, ct);
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            ReadResult = data.Length > 0 ? ToHex(data) : "(空响应)";
            _log.Success(device.Name, $"读取 {address}×{length} → {data.Length} 字节, {sw.ElapsedMilliseconds} ms");
            _log.Rx(device.Name, ReadResult);
        });
    }

    private async Task WriteSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        var address = WriteAddress.Trim();
        if (!TryParseHex(WriteData, out var data))
        {
            _log.Warning(device.Name, "写入数据不是合法的十六进制");
            return;
        }

        await RunDeviceOperation(device, async ct =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ok = await device.Device.WriteAsync(address, data, ct);
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            _log.Success(device.Name, $"写入 {address} → {(ok ? "成功" : "失败")}, {sw.ElapsedMilliseconds} ms");
            _log.Tx(device.Name, ToHex(data));
        });
    }

    private async Task SendRawSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        if (!TryParsePayload(RawInput, out var payload))
        {
            _log.Warning(device.Name, "发送内容格式无效（十六进制或文本模式）");
            return;
        }

        await RunDeviceOperation(device, async ct =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ok = await device.Device.SendAsync(payload, ct);
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            _log.Tx(device.Name, ToDisplay(payload));
            _log.Success(device.Name, ok ? $"发送成功, {sw.ElapsedMilliseconds} ms" : "发送失败");
        });
    }

    private async Task SendAndReceiveSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        if (!TryParsePayload(RawInput, out var payload))
        {
            _log.Warning(device.Name, "发送内容格式无效（十六进制或文本模式）");
            return;
        }

        await RunDeviceOperation(device, async ct =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await device.Device.SendAndReceiveAsync(payload, ct);
            sw.Stop();
            LastElapsed = $"{sw.ElapsedMilliseconds} ms";
            _lastResponse = response;
            RawResult = ToDisplayRx(response);
            _log.Tx(device.Name, ToDisplay(payload));
            _log.Rx(device.Name, $"{RawResult} ({sw.ElapsedMilliseconds} ms)");
        });
    }

    // ===== 辅助 =====

    /// <summary>单设备操作：可取消（超时/取消按钮），耗时计入 LastElapsed，取消与失败分别记录。</summary>
    private async Task RunDeviceOperation(DeviceViewModel device, Func<CancellationToken, Task> operation)
    {
        device.IsBusy = true;
        IsBusy = true;
        StatusHint = $"{device.Name} 执行中…（可取消）";
        _operationCts = new CancellationTokenSource();
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
            device.IsBusy = false;
            _operationCts?.Dispose();
            _operationCts = null;
            IsBusy = false;
            UpdateSummary();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool FilterLog(object item)
    {
        if (item is not LogEntry entry)
        {
            return false;
        }
        if (_logLevelFilter != "全部" && entry.LevelText.Trim() != _logLevelFilter)
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(_logKeyword))
        {
            var keyword = _logKeyword.Trim();
            if (!entry.Source.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                && !entry.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    private void RefreshLogFilter() =>
        CollectionViewSource.GetDefaultView(Logs).Refresh();

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

    private async Task LoadConfigAsync()
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
                var device = _session.RestoreDevice(param);
                var vm = new DeviceViewModel(device);
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DeviceViewModel.IsConnected))
                    {
                        UpdateSummary();
                    }
                };
                Devices.Add(vm);
                restored++;
            }
            catch (Exception ex)
            {
                _log.Error("Session", $"恢复 [{param.DeviceName}] 失败: {ex.Message}");
                skipped++;
            }
        }
        if (Devices.Count > 0 && SelectedDevice is null)
        {
            SelectedDevice = Devices[0];
        }
        UpdateSummary();
        _log.Success("Session", $"配置恢复 {restored} 台，跳过 {skipped} 条");
        await Task.CompletedTask;
    }

    private void UpdateOperationHints()
    {
        OnPropertyChanged(nameof(AddressHint));
        OnPropertyChanged(nameof(HasDevice));
        OnPropertyChanged(nameof(DeviceSupportsAddress));
        OnPropertyChanged(nameof(DeviceSupportsRaw));
        if (SelectedDevice is not null)
        {
            ReadAddress = SelectedDevice.Protocol switch
            {
                "Modbus TCP" or "Modbus RTU" => "HR0",
                "三菱 MC" => "D100",
                "OPC UA" => "ns=2;s=Tag1",
                "HTTP" => "/api/data",
                "Web API" => "/api/v1/data",
                _ => ReadAddress,
            };
            WriteAddress = ReadAddress;
        }
    }

    private bool TryParsePayload(string input, out byte[] payload)
    {
        if (UseHex)
        {
            return TryParseHex(input, out payload);
        }

        payload = Encoding.UTF8.GetBytes(input);
        return true;
    }

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

    private static bool TryParseLength(string input, out int length)
    {
        return int.TryParse(input.Trim(), out length) && length > 0;
    }

    private static string ToHex(byte[] data) =>
        data.Length == 0 ? string.Empty : BitConverter.ToString(data).Replace("-", " ");

    /// <summary>收发统一显示：跟随 HEX 开关。</summary>
    private string ToDisplayRx(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        return UseHex ? ToHex(data) : Encoding.UTF8.GetString(data);
    }

    private string ToDisplay(byte[] data) => ToDisplayRx(data);

    private void OnRxCounted(string source, string message)
    {
        _dispatcher.BeginInvoke(() =>
        {
            _rxCount++;
            RxCountText = _rxCount.ToString("N0");
        });
    }

    private void OnTxCounted(string source, string message)
    {
        _dispatcher.BeginInvoke(() =>
        {
            _txCount++;
            TxCountText = _txCount.ToString("N0");
        });
    }

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
        return true;
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>轻量命令封装。</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}
