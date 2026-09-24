using System.Threading;
using System.Windows.Threading;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Controls.Models;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// 相机测试页实际流程：档案读盘/兜底、驱动目录、实例指纹重建、连接/断开都在
/// <see cref="DeviceWorkspaceBase{TModel, TProfile}"/>，此处只留相机专属：
/// 实时预览循环（100ms 定时器）、单帧抓图回填。
/// 只依赖 Device.Core 抽象与工厂契约，不引用 Device.Runtime。
/// </summary>
public sealed class CameraWorkspace : DeviceWorkspaceBase<CameraPageModel, CameraProfile>
{
    private readonly ICameraFactory _factory;
    private DispatcherTimer? _liveTimer;
    private int _liveBusy;

    public CameraWorkspace(ICameraFactory factory)
        : base(factory.AvailableDrivers)
    {
        _factory = factory;

        // 打开页面时测试卡要显示「当前选中档案」的触发配置，而不是 Model 的初始默认值。
        SyncRuntimeTriggerFromProfile();
    }

    protected override string CategoryName => "相机";

    protected override string FallbackProfileName => "相机01";

    /// <summary>
    /// 档案指纹：驱动 + 扫描类型 + IP + 参数文件 + 全部参数（含触发）任一改动都重建实例
    /// （曝光/增益/触发在连接时下发）。触发必须进指纹，否则改了触发配置不会重建实例，等于没改。
    /// </summary>
    protected override string BuildKey(CameraProfile profile) =>
        $"{profile.DriverId}:{profile.ScanType}:{profile.IpAddress}:{profile.ParameterFilePath}:{profile.ExposureMilliseconds}:{profile.Gain}:{profile.TimeoutMilliseconds}:{profile.FrameTriggerMode}:{profile.FrameTriggerSource}:{profile.LineTriggerMode}:{profile.LineTriggerSource}";

    /// <summary>实例创建由工厂延迟触发，Workbench 注册工厂时不会预先创建各品牌相机；此步骤不连接硬件。</summary>
    protected override IDevice CreateDevice(CameraProfile profile) =>
        _factory.Create(profile.ToOptions());

    public override async Task ConnectAsync()
    {
        StopLive();
        await base.ConnectAsync();

        // 连接后把测试卡拉回档案值：上次连接的临时改动不应跨连接残留。
        SyncRuntimeTriggerFromProfile();
    }

    public override async Task DisconnectAsync()
    {
        StopLive();
        await base.DisconnectAsync();
    }

    /// <summary>选中行切换（ConfigManagerView SelectedCallback → 对标 CamManagerService.SetCurCam）。</summary>
    public override void SetCurDevice()
    {
        StopLive();
        SyncRuntimeTriggerFromProfile();
    }

    /// <summary>
    /// 把测试卡上的触发配置下发到已连接的相机。运行时覆盖，不写档案
    /// —— 档案里的值仍是产线工况，测试完断开重连即回到档案值。
    /// </summary>
    public async Task ApplyTriggerAsync()
    {
        var camera = Connected<ICamera>();
        var result = await camera
            .SetTriggerAsync(Model.BuildRuntimeFrameTrigger(), Model.BuildRuntimeLineTrigger())
            .ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "下发触发配置失败");
    }

    /// <summary>切行/连接后把档案的触发配置播种到测试卡（测试卡改动只作用于当前连接）。</summary>
    private void SyncRuntimeTriggerFromProfile()
    {
        var profile = Model.SelectedProfile;
        if (profile is null)
            return;

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.RuntimeFrameTriggerMode = profile.FrameTriggerMode;
            Model.RuntimeFrameTriggerSource = profile.FrameTriggerSource;
            Model.RuntimeLineTriggerMode = profile.LineTriggerMode;
            Model.RuntimeLineTriggerSource = profile.LineTriggerSource;
            Model.RefreshLineTriggerAvailability();
        });
    }

    /// <summary>触发一次采集并回填预览。</summary>
    public async Task CaptureAsync()
    {
        var camera = Connected<ICamera>();
        var frame = await camera.CaptureAsync().ConfigureAwait(false);
        var image = ImageFrameRenderer.Render(frame);

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.Preview = image;
            Model.FrameInfo = $"{frame.Width}×{frame.Height} {frame.PixelFormat}";
        });
    }

    /// <summary>实时预览开关（对标 CamManagerService.Live：定时器循环抓图回填）。</summary>
    public void ToggleLive()
    {
        if (_liveTimer is not null)
        {
            StopLive();
            return;
        }

        Connected<ICamera>();
        _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _liveTimer.Tick += (_, _) => LiveTick();
        _liveTimer.Start();
        Model.LiveButtonText = "实时关闭";
    }

    /// <summary>停止实时预览（抓图/连接/断开/切行路径都会调用，可重入）。</summary>
    public void StopLive()
    {
        if (_liveTimer is null)
            return;

        _liveTimer.Stop();
        _liveTimer = null;
        Model.LiveButtonText = "图像实时";
    }

    private void LiveTick()
    {
        // 上一帧未回来就跳过本拍，避免实时循环自堆叠。
        if (Interlocked.CompareExchange(ref _liveBusy, 1, 0) != 0)
            return;

        _ = RunLiveFrameAsync();
    }

    private async Task RunLiveFrameAsync()
    {
        try
        {
            await CaptureAsync().ConfigureAwait(false);
        }
        catch
        {
            // 实时循环里失败即静默停止（相机被拔出/参数错误），用户可再点抓图定位原因。
            DispatcherEx.UIInvokeSafe(StopLive);
        }
        finally
        {
            Interlocked.Exchange(ref _liveBusy, 0);
        }
    }
}
