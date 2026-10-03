// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DeviceViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Controls、CommunityToolkit.Mvvm.ComponentModel、Communication.Controls.Services、Communication.Core、Communication.Runtime.Core、MaterialDesignThemes.Wpf。导入不代表所有类型都会在运行时加载。
using Communication.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Communication.Controls.Services;
using Communication.Core;
using Communication.Runtime.Core;
using MaterialDesignThemes.Wpf;

namespace Communication.Controls.ViewModels;

/// <summary>设备列表行视图模型：只反映设备状态，不做 IO。</summary>
public sealed partial class DeviceViewModel : ObservableObject, IDisposable
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

    /// <summary>
    /// 初始化 DeviceViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Describe；具体条件和先后次序见方法体。
    /// </summary>
    public DeviceViewModel(ICommunicationDevice device)
    {
        _device = device;
        Info = ProtocolCatalog.Describe(device.ProtocolType);
        IsConnected = device.IsConnected;
        StatusText = device.IsConnected ? "已连接" : "未连接";
        StatusShort = StatusText;
        device.ConnectionStateChanged += OnConnectionChanged;
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
    private bool _disposed;
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnConnectionChanged。
    /// 正文中的关键协作调用包括 UIInvokeSafe、IsNullOrWhiteSpace、ShortenStatus；具体条件和先后次序见方法体。
    /// </summary>
    private void OnConnectionChanged(object? sender, Communication.Core.EventArgs.ConnectionStateChangedEventArgs e)
    {
        if (_disposed) return;
        Extension.UI.Controls.DispatcherEx.UIInvokeSafe(() =>
        {
            if (_disposed) return;

            IsConnected = e.IsConnected;
            StatusText = string.IsNullOrWhiteSpace(e.Message)
                ? (e.IsConnected ? "已连接" : "未连接")
                : e.Message;
            StatusShort = e.IsConnected ? ShortenStatus(StatusText) : "未连接";
        });
    }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _device.ConnectionStateChanged -= OnConnectionChanged;
    }

}
