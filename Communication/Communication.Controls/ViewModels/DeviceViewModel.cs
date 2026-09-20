using CommunityToolkit.Mvvm.ComponentModel;
using Communication.Controls.Services;
using CommunicationNew.Core;
using MaterialDesignThemes.Wpf;

namespace Communication.Controls.ViewModels;

/// <summary>设备列表行视图模型：只反映设备状态，不做 IO。</summary>
public sealed partial class DeviceViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "未连接";

    [ObservableProperty]
    private string statusShort = "未连接";

    private readonly ICommunicationDevice _device;

    public DeviceViewModel(ICommunicationDevice device)
    {
        _device = device;
        Info = ProtocolCatalog.Describe(device.ProtocolType);
        IsConnected = device.IsConnected;
        StatusText = device.IsConnected ? "已连接" : "未连接";
        StatusShort = StatusText;
        device.ConnectionStateChanged += (_, e) =>
        {
            IsConnected = e.IsConnected;
            StatusText = string.IsNullOrWhiteSpace(e.Message)
                ? (e.IsConnected ? "已连接" : "未连接")
                : e.Message;
            StatusShort = e.IsConnected ? ShortenStatus(StatusText) : "未连接";
        };
    }

    /// <summary>
    /// 协议层消息形如 "Modbus TCP客户端已连接: 127.0.0.1:502" 或 "已连接 127.0.0.1:18123"。
    /// 行首的圆点已经表示连接状态，这里只留端点，否则端口会被截成 "127.0.0.1:1"。
    /// </summary>
    private static string ShortenStatus(string text)
    {
        var cut = text.IndexOf("已连接", StringComparison.Ordinal);
        if (cut >= 0)
        {
            text = text[(cut + "已连接".Length)..];
        }

        var colon = text.IndexOf(": ", StringComparison.Ordinal);
        if (colon >= 0)
        {
            text = text[(colon + 2)..];
        }

        return text.Trim();
    }

    public ICommunicationDevice Device => _device;

    public string Name => _device.DeviceName;

    public string Id => _device.DeviceId;

    public string Protocol => Info.DisplayName;

    public PackIconKind ProtocolIcon => Info.Icon;

    public string RoleText => _device.Role switch
    {
        DeviceRole.Client => "客户端",
        DeviceRole.Server => "服务端",
        _ => "—",
    };

    public ProtocolCatalog.ProtocolInfo Info { get; }

    /// <summary>协议枚举（页面上按协议切换地址提示）。</summary>
    public ProtocolType ProtocolType => _device.ProtocolType;
}
