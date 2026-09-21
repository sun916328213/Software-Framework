using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using WPFCustomControls.Controls;

namespace Device.Controls.Models;

/// <summary>设备档案的共同形状：页面基类需要按 Name/DriverId 做去重、回填与指纹。</summary>
public interface IDeviceProfile
{
    string Name { get; set; }
    string DriverId { get; set; }
}

/// <summary>驱动下拉项（Value = DriverId 字符串，DisplayName = 工厂描述名）。</summary>
public sealed record DeviceDriverOption(string Value, string DisplayName);

/// <summary>
/// 设备页状态 Model 基类：档案集合 + 驱动目录 + 连接状态（一页一 JSON 文件约定）。
/// 各类别页在其上追加自己的展示状态。
/// </summary>
public abstract partial class DevicePageModelBase<TProfile> : ConfigManagerModel<TProfile>
    where TProfile : class, IDeviceProfile
{
    protected DevicePageModelBase(string fileName)
    {
        FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        FileName = fileName;
    }

    [ObservableProperty]
    private TProfile? selectedProfile;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string statusText = "未连接";

    partial void OnSelectedProfileChanged(TProfile? value)
    {
        // 档案切换 = 连接要素变更，状态复位；实例由流程按指纹重建。
        IsConnected = false;
        StatusText = "未连接";
    }

    /// <summary>驱动下拉数据（来自工厂 AvailableDrivers）。</summary>
    public ObservableCollection<DeviceDriverOption> Drivers { get; } = [];

    /// <summary>当前档案（空即报“请先添加”）。</summary>
    public TProfile SelectedProfileOrThrow() =>
        SelectedProfile ?? throw new InvalidOperationException("请先添加设备档案");
}
