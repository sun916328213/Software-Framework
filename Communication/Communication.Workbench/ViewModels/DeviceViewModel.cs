using System.ComponentModel;
using System.Windows.Media;
using CommunicationNew.Core;
using Communication.Workbench.Services;

namespace Communication.Workbench.ViewModels;

/// <summary>设备列表行视图模型。</summary>
public sealed class DeviceViewModel : INotifyPropertyChanged
{
    private readonly ICommunicationDevice _device;
    private readonly ProtocolCatalog.ProtocolInfo _info;
    private bool _isConnected;
    private bool _isBusy;
    private string _statusText = "未连接";

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeviceViewModel(ICommunicationDevice device)
    {
        _device = device;
        _info = ProtocolCatalog.Describe(device.ProtocolType);
        _isConnected = device.IsConnected;
        device.ConnectionStateChanged += (_, e) =>
        {
            IsConnected = e.IsConnected;
            StatusText = e.IsConnected ? "已连接" : "未连接";
        };
    }

    public ICommunicationDevice Device => _device;

    public string Name => _device.DeviceName;

    public string Id => _device.DeviceId;

    public string Protocol => _info.DisplayName;

    public string ProtocolIcon => _info.Icon;

    public string RoleText => _device.Role switch
    {
        DeviceRole.Client => "客户端",
        DeviceRole.Server => "服务端",
        _ => "—",
    };

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetField(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionColor));
                OnPropertyChanged(nameof(IsNotConnected));
            }
        }
    }

    public bool IsNotConnected => !IsConnected;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public Brush ConnectionColor => IsConnected
        ? (Brush)App.Current.FindResource("Brush.Success")
        : (Brush)App.Current.FindResource("Brush.Fg.Muted");

    public ProtocolCatalog.ProtocolInfo Info => _info;

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name!);
        return true;
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
