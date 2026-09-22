using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Cameras;
using Device.Core.Devices;

namespace Device.Controls.Models;

/// <summary>
/// 相机档案（相机列表的可编辑行；创建相机时转换成 <see cref="CameraOptions"/>）。
/// Options 是 Device.Core 的不可变创建参数，不带变更通知、不能直接绑表格，
/// 所以 DataGrid 行/落盘 JSON 用本类。
/// </summary>
public partial class CameraProfile : ObservableObject, IDeviceProfile
{
    /// <summary>档案在应用中的唯一名称。</summary>
    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>驱动标识字符串（配置界面从工厂 AvailableDrivers 选择，不手填）。</summary>
    [ObservableProperty]
    private string driverId = string.Empty;

    /// <summary>扫描类型（面阵/线扫），决定驱动的触发器下发方式。</summary>
    [ObservableProperty]
    private CameraScanType scanType = CameraScanType.AreaScan;

    /// <summary>相机 IP 地址。</summary>
    [ObservableProperty]
    private string ipAddress = "127.0.0.1";

    /// <summary>曝光时间，单位为毫秒。</summary>
    [ObservableProperty]
    private int exposureMilliseconds = 20;

    /// <summary>相机增益。</summary>
    [ObservableProperty]
    private double gain;

    /// <summary>采集超时时间，单位为毫秒。</summary>
    [ObservableProperty]
    private int timeoutMilliseconds = 1_000;

    /// <summary>厂商参数文件路径（连接相机后先加载该文件再应用公共参数）；空 = 不加载。</summary>
    [ObservableProperty]
    private string parameterFilePath = string.Empty;

    /// <summary>转换为运行时创建相机用的不可变配置。</summary>
  
     public CameraOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        ScanType = ScanType,
        IpAddress = IpAddress,
        ParameterFilePath = string.IsNullOrWhiteSpace(parameterFilePath) ? null : parameterFilePath,
        ExposureMilliseconds = ExposureMilliseconds,
        Gain = Gain,
        TimeoutMilliseconds = TimeoutMilliseconds,
    }; 
}

/// <summary>相机测试页状态：连接/档案/驱动目录走通用基类，此处只加预览图像与实时按钮文字。</summary>
public partial class CameraPageModel : DevicePageModelBase<CameraProfile>
{
    public CameraPageModel()
        : base("Cameras.json")
    {
    }

    /// <summary>
    /// 扫描类型下拉数据（枚举名直出，不做中文映射）。静态：DataGridComboBoxColumn
    /// 不在可视树里，只能 {x:Static} 绑定，拿不到实例属性。
    /// </summary>
    public static IReadOnlyList<CameraScanType> ScanTypes { get; } =
    [
        CameraScanType.AreaScan,
        CameraScanType.LineScan,
    ];

    /// <summary>实时预览按钮文字（图像实时 ↔ 实时关闭，对标 CamManagerModel）。</summary>
    [ObservableProperty]
    private string liveButtonText = "图像实时";

    /// <summary>最近一次抓图的渲染结果。</summary>
    [ObservableProperty]
    private ImageSource? preview;

    /// <summary>最近一次抓图的规格说明（尺寸 + 像素格式）。</summary>
    [ObservableProperty]
    private string frameInfo = "尚无图像";
}
