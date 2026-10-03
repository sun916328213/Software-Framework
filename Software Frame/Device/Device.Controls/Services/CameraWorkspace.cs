// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CameraWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System.Threading、System.Windows.Threading、Device.Core.Cameras、Device.Core.Devices、Device.Controls.Models。导入不代表所有类型都会在运行时加载。
using Device.Controls;
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
    private Task? _liveTask;

    /// <summary>
    /// 初始化 CameraWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 SyncRuntimeTriggerFromProfile；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 StopLive、ConnectAsync、SyncRuntimeTriggerFromProfile；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public override async Task ConnectAsync()
    {
        StopLive();
        await base.ConnectAsync();

        // 连接后把测试卡拉回档案值：上次连接的临时改动不应跨连接残留。
        SyncRuntimeTriggerFromProfile();
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 StopLive、DisconnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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
            if (IsClosed) return;
            Model.Preview = image;
            Model.FrameInfo = $"{frame.Width}×{frame.Height} {frame.PixelFormat}";
        });
    }

    /// <summary>实时预览开关（对标 CamManagerService.Live：定时器循环抓图回填）。</summary>
    public void ToggleLive()
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
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

    /// <summary>Stop scheduling frames and drain the last capture before releasing the camera.</summary>
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 StopLive、DisposeAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async ValueTask DisposeAsync()
    {
        StopLive();
        if (_liveTask is { } task) await task;
        await base.DisposeAsync();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 LiveTick。
    /// 正文中的关键协作调用包括 CompareExchange、RunLiveFrameAsync；具体条件和先后次序见方法体。
    /// </summary>
    private void LiveTick()
    {
        // 上一帧未回来就跳过本拍，避免实时循环自堆叠。
        if (Interlocked.CompareExchange(ref _liveBusy, 1, 0) != 0)
            return;

        _liveTask = RunLiveFrameAsync();
    }

    /// <summary>
    /// 运行当前处理流程；对应入口为 RunLiveFrameAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、CaptureAsync、UIInvokeSafe、Exchange；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
