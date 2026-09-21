using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Cameras;
using WPFCustomControls.Controls;

namespace Device.Controls.Models;

/// <summary>
/// 相机测试页状态 Model：档案集合与落盘走通用配置管理模型基类
/// （<see cref="ConfigManagerModel{T}.DataCollection"/> / <see cref="ConfigManagerModel{T}.FullPath"/>），
/// 连接状态、扫描结果与预览图像为本页扩展。
/// </summary>
public partial class CameraPageModel : ConfigManagerModel<CameraProfile>
{
    public CameraPageModel()
    {
        // 落盘位置一次给定（与 Sql/Log/Excel 各页「一页一文件」约定一致）。
        FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        FileName = "Cameras.json";
    }

    [ObservableProperty]
    private CameraProfile? selectedProfile;

    partial void OnSelectedProfileChanged(CameraProfile? value)
    {
        // 档案切换 = 连接要素变更，状态复位；实例由流程按指纹重建。
        IsConnected = false;
        StatusText = "未连接";
    }

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string statusText = "未连接";

    /// <summary>实时预览按钮文字（图像实时 ↔ 实时关闭，对标 CamManagerModel）。</summary>
    [ObservableProperty]
    private string liveButtonText = "图像实时";

    /// <summary>中心十字线按钮文字。</summary>
    [ObservableProperty]
    private string showCrossButtonText = "显示中心十字线";

    /// <summary>预览区是否叠加中心十字线。</summary>
    [ObservableProperty]
    private bool isShowCross;

    /// <summary>最近一次抓图的渲染结果。</summary>
    [ObservableProperty]
    private ImageSource? preview;

    /// <summary>最近一次抓图的规格说明（尺寸 + 像素格式）。</summary>
    [ObservableProperty]
    private string frameInfo = "尚无图像";

    /// <summary>扫描结果选中项。</summary>
    [ObservableProperty]
    private CameraDeviceDescriptor? selectedDevice;

    /// <summary>驱动下拉数据（来自工厂 AvailableDrivers）。</summary>
    public ObservableCollection<CameraDriverOption> Drivers { get; } = [];

    /// <summary>最近一次扫描发现的在线相机。</summary>
    public ObservableCollection<CameraDeviceDescriptor> Devices { get; } = [];

    /// <summary>当前档案（空即报“请先添加”）。</summary>
    public CameraProfile SelectedProfileOrThrow() =>
        SelectedProfile ?? throw new InvalidOperationException("请先添加相机档案");

    /// <summary>当前扫描选中设备。</summary>
    public CameraDeviceDescriptor SelectedDeviceOrThrow() =>
        SelectedDevice ?? throw new InvalidOperationException("请先在在线设备列表中选中一台相机");
}
