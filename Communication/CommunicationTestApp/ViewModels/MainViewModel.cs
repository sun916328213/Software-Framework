using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunicationNew.Core;
using CommunicationTestApp.Services;
using CommunicationTestApp.Views;

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
    }

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

    public bool HasDevice => SelectedDevice is not null;

    public bool DeviceSupportsAddress => SelectedDevice is { Info.SupportsAddressRead: true };

    public bool DeviceSupportsRaw => SelectedDevice is not null
        && SelectedDevice.Device.ProtocolType is ProtocolType.Tcp
            or ProtocolType.Udp
            or ProtocolType.Serial
            or ProtocolType.Http;

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

    public ICommand ClearLogCommand => new RelayCommand(_ => _log.Clear());

    public ICommand ResetCountersCommand => new RelayCommand(() =>
    {
        _rxCount = 0;
        _txCount = 0;
        RxCountText = "0";
        TxCountText = "0";
    });

    /// <summary>黑白主题切换。</summary>
    public ICommand ToggleThemeCommand => new RelayCommand(() =>
    {
        _theme.Toggle();
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeIcon));
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

                var device = _session.CreateDevice(param);
                var vm = new DeviceViewModel(device);
                Devices.Add(vm);
                SelectedDevice = vm;
            }));
    }

    private async Task ConnectSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        device.IsBusy = true;
        try
        {
            await RunDeviceOperation(device, () => _session.ConnectDeviceAsync(device.Device));
        }
        finally
        {
            device.IsBusy = false;
        }
    }

    private async Task DisconnectSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        device.IsBusy = true;
        try
        {
            await RunDeviceOperation(device, () => _session.DisconnectDeviceAsync(device.Device));
        }
        finally
        {
            device.IsBusy = false;
        }
    }

    private async Task RemoveSelectedAsync()
    {
        if (SelectedDevice is not { } device)
        {
            return;
        }

        var index = Devices.IndexOf(device);
        await RunDeviceOperation(device, () => _session.RemoveDeviceAsync(device.Id));
        Devices.Remove(device);
        SelectedDevice = Devices.Count > 0
            ? Devices[Math.Clamp(index, 0, Devices.Count - 1)]
            : null;
    }

    private async Task ReadSelectedAsync()
    {
        if (SelectedDevice is not { } device || !TryParseLength(ReadLength, out var length))
        {
            return;
        }

        var address = ReadAddress.Trim();
        await RunDeviceOperation(device, async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var data = await device.Device.ReadAsync(address, length);
            sw.Stop();
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

        await RunDeviceOperation(device, async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ok = await device.Device.WriteAsync(address, data);
            sw.Stop();
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

        await RunDeviceOperation(device, async () =>
        {
            var ok = await device.Device.SendAsync(payload);
            _log.Tx(device.Name, ToDisplay(payload));
            _log.Success(device.Name, ok ? "发送成功" : "发送失败");
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

        await RunDeviceOperation(device, async () =>
        {
            var response = await device.Device.SendAndReceiveAsync(payload);
            _lastResponse = response;
            RawResult = ToDisplayRx(response);
            _log.Tx(device.Name, ToDisplay(payload));
            _log.Rx(device.Name, RawResult);
        });
    }

    // ===== 辅助 =====

    private async Task RunDeviceOperation(DeviceViewModel device, Func<Task> operation)
    {
        device.IsBusy = true;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            _log.Error(device.Name, ex.Message);
        }
        finally
        {
            device.IsBusy = false;
        }
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
