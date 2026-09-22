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

    /// <summary>帧触发模式（面阵与线扫相机都会下发）。</summary>
    [ObservableProperty]
    private CameraTriggerMode frameTriggerMode = CameraTriggerMode.On;

    /// <summary>帧触发源（GenICam 符号；启用触发时不能为空）。</summary>
    [ObservableProperty]
    private string frameTriggerSource = CameraTriggerSources.Software;

    /// <summary>行触发模式（仅线扫相机下发，面阵相机忽略）。</summary>
    [ObservableProperty]
    private CameraTriggerMode lineTriggerMode = CameraTriggerMode.Off;

    /// <summary>行触发源（仅线扫相机下发）。</summary>
    [ObservableProperty]
    private string lineTriggerSource = CameraTriggerSources.Line1;

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
        FrameTrigger = BuildFrameTrigger(),
        LineTrigger = BuildLineTrigger(),
    };

    /// <summary>本档案的帧触发配置（选择器留空 = 保留相机当前选择器）。</summary>
    public CameraTriggerSettings BuildFrameTrigger() => new()
    {
        Mode = FrameTriggerMode,
        Source = FrameTriggerSource,
    };

    /// <summary>本档案的行触发配置（线扫相机用 LineStart 选择器）。</summary>
    public CameraTriggerSettings BuildLineTrigger() => new()
    {
        Selector = CameraTriggerSelectors.LineStart,
        Mode = LineTriggerMode,
        Source = LineTriggerSource,
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

    /// <summary>
    /// 触发模式下拉数据（关/开）。静态：DataGridComboBoxColumn 与测试卡下拉都用它，
    /// 与 ScanTypes 同一约定 —— 枚举名直出，不做中文映射。
    /// </summary>
    public static IReadOnlyList<CameraTriggerMode> TriggerModes { get; } =
    [
        CameraTriggerMode.Off,
        CameraTriggerMode.On,
    ];

    /// <summary>
    /// 触发源下拉数据。只列常用 GenICam 符号；型号专有触发源请写进厂商参数文件
    /// （CameraTriggerSources 的注释明确说该列表不追求穷尽）。
    /// </summary>
    public static IReadOnlyList<string> TriggerSources { get; } =
    [
        CameraTriggerSources.Software,
        CameraTriggerSources.Line0,
        CameraTriggerSources.Line1,
        CameraTriggerSources.Line2,
        CameraTriggerSources.Line3,
        CameraTriggerSources.RotaryEncoder1,
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

    // ---------- 测试卡上的运行时触发（改完点「下发触发」生效；只作用于当前连接，不写档案） ----------

    /// <summary>测试卡上的帧触发模式；连接/切行时从档案播种。</summary>
    [ObservableProperty]
    private CameraTriggerMode runtimeFrameTriggerMode = CameraTriggerMode.On;

    /// <summary>测试卡上的帧触发源；连接/切行时从档案播种。</summary>
    [ObservableProperty]
    private string runtimeFrameTriggerSource = CameraTriggerSources.Software;

    /// <summary>测试卡上的行触发模式；连接/切行时从档案播种。</summary>
    [ObservableProperty]
    private CameraTriggerMode runtimeLineTriggerMode = CameraTriggerMode.Off;

    /// <summary>测试卡上的行触发源；连接/切行时从档案播种。</summary>
    [ObservableProperty]
    private string runtimeLineTriggerSource = CameraTriggerSources.Line1;

    /// <summary>
    /// 行触发控件是否可用。只有线扫相机才会下发 LineStart 触发，面阵相机忽略，
    /// 故面阵时禁用这两项，避免「改了没反应」的误判。
    /// </summary>
    public bool IsLineTriggerEnabled => SelectedProfile?.ScanType == CameraScanType.LineScan;

    /// <summary>测试卡上的帧触发配置（运行时下发用）。</summary>
    public CameraTriggerSettings BuildRuntimeFrameTrigger() => new()
    {
        Mode = RuntimeFrameTriggerMode,
        Source = RuntimeFrameTriggerSource,
    };

    /// <summary>测试卡上的行触发配置（运行时下发用）。</summary>
    public CameraTriggerSettings BuildRuntimeLineTrigger() => new()
    {
        Selector = CameraTriggerSelectors.LineStart,
        Mode = RuntimeLineTriggerMode,
        Source = RuntimeLineTriggerSource,
    };

    /// <summary>
    /// 通知 <see cref="IsLineTriggerEnabled"/> 重新求值。切行/连接后由 Workspace 调用
    /// —— 该属性依赖基类的 SelectedProfile，而基类的变更钩子是 partial 方法，派生类接不上。
    /// </summary>
    public void RefreshLineTriggerAvailability() => OnPropertyChanged(nameof(IsLineTriggerEnabled));
}
