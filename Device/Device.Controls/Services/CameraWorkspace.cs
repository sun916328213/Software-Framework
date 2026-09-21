using System.Threading;
using System.Windows.Threading;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Controls.Models;
using Extension.Core.Conversion;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// 相机测试页实际流程（对标 DataManager.Controls 的 SqlWorkspace：持有 <see cref="CameraPageModel"/>
/// 并读写它，ViewModel 只暴露 Model + 装配命令）。
/// 职责：档案初始化（读 Config → 默认）、相机实例生命周期（指纹重建）、
/// 连接/断开/抓图/扫描/参数下发与预览回填。
/// 只依赖 Device.Core 抽象与工厂契约，不引用 Device.Drivers。
/// </summary>
public sealed class CameraWorkspace
{
    private readonly ICameraFactory _factory;
    private ICamera? _camera;
    private string? _currentKey;
    private DispatcherTimer? _liveTimer;
    private int _liveBusy;

    /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
    public CameraPageModel Model { get; } = new();

    public CameraWorkspace(ICameraFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        foreach (var descriptor in factory.AvailableDrivers)
            Model.Drivers.Add(new CameraDriverOption(descriptor.Id.Value, descriptor.DisplayName));

        if (Model.Drivers.Count == 0)
            throw new InvalidOperationException("未注册任何相机驱动，无法初始化相机配置。");

        LoadProfiles();
    }

    /// <summary>档案指纹：驱动 + IP + 全部参数任一改动都重建实例（曝光/增益在连接时下发）。</summary>
    public static string BuildKey(CameraProfile profile) =>
        $"{profile.DriverId}:{profile.IpAddress}:{profile.ExposureMilliseconds}:{profile.Gain}:{profile.TimeoutMilliseconds}";

    /// <summary>
    /// 由 Controls 根据当前档案创建相机实例；指纹命中时复用，否则释放旧实例后，
    /// 通过统一工厂只创建所选 DriverId 对应的相机。此步骤不会连接硬件。
    /// </summary>
    public async ValueTask<ICamera> EnsureCameraAsync(CameraProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var key = BuildKey(profile);
        if (_camera is not null && _currentKey == key)
            return _camera;

        await ReleaseCameraAsync().ConfigureAwait(false);
        // 实例创建由 Controls 在实际使用时触发，Workbench 注册工厂时不会预先创建各品牌相机。
        _camera = _factory.Create(profile.ToOptions());
        _currentKey = key;
        return _camera;
    }

    public async Task ConnectAsync()
    {
        StopLive();
        var camera = await EnsureCameraAsync(Model.SelectedProfileOrThrow()).ConfigureAwait(false);
        var result = await camera.ConnectAsync().ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "连接失败");

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.IsConnected = true;
            Model.StatusText = "已连接";
        });
    }

    public async Task DisconnectAsync()
    {
        StopLive();
        if (_camera is null)
            return;

        var result = await _camera.DisconnectAsync().ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "断开失败");

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.IsConnected = false;
            Model.StatusText = "未连接";
        });
    }

    /// <summary>触发一次采集并回填预览。</summary>
    public async Task CaptureAsync()
    {
        var camera = EnsureConnectedCamera();
        var frame = await camera.CaptureAsync().ConfigureAwait(false);
        var image = ImageFrameRenderer.Render(frame);

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.Preview = image;
            Model.FrameInfo = $"{frame.Width}×{frame.Height} {frame.PixelFormat}";
        });
    }

    /// <summary>扫描在线相机并回填设备列表；返回需要提示的警告文本（部分驱动失败），无警告返回 null。</summary>
    public async Task<string?> ScanDevicesAsync()
    {
        var result = await _factory.EnumerateDevicesAsync().ConfigureAwait(false);

        DispatcherEx.UIInvokeSafe(() =>
        {
            Model.Devices.Clear();
            foreach (var device in result.Value ?? [])
                Model.Devices.Add(device);
            Model.SelectedDevice = Model.Devices.FirstOrDefault();
        });

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? "枚举在线相机失败");

        return result.ErrorCode == "camera.enumerate_partial"
            ? $"部分驱动枚举失败：{result.Message}"
            : null;
    }

    /// <summary>选中行切换（ConfigManagerView SelectedCallback → 对标 CamManagerService.SetCurCam）。</summary>
    public void SetCurCam() => StopLive();

    /// <summary>实时预览开关（对标 CamManagerService.Live：定时器循环抓图回填）。</summary>
    public void ToggleLive()
    {
        if (_liveTimer is not null)
        {
            StopLive();
            return;
        }

        EnsureConnectedCamera();
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

    /// <summary>用扫描选中的设备创建档案（名称去重；IP/驱动回填）。</summary>
    public void AddProfileFromDevice()
    {
        var device = Model.SelectedDeviceOrThrow();
        var name = device.DisplayName;
        var suffix = 1;
        while (Model.DataCollection.Any(p => p.Name == name))
            name = $"{device.DisplayName}_{suffix++}";

        var profile = new CameraProfile
        {
            Name = name,
            DriverId = device.DriverId.Value,
            IpAddress = string.IsNullOrWhiteSpace(device.IpAddress) ? "127.0.0.1" : device.IpAddress,
        };
        Model.DataCollection.Add(profile);
        Model.SelectedProfile = profile;
    }

    private ICamera EnsureConnectedCamera()
    {
        if (_camera is null || _camera.State != DeviceState.Connected)
            throw new InvalidOperationException("请先连接相机");
        return _camera;
    }

    private async ValueTask ReleaseCameraAsync()
    {
        if (_camera is null)
            return;

        try
        {
            await _camera.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // 重建路径上的释放失败不阻断新实例创建。
        }

        _camera = null;
        _currentKey = null;
    }

    // ---------- Model 初始化（读 Config 文件 → 默认） ----------
    private void LoadProfiles()
    {
        var profiles = JsonEx.TryRead<List<CameraProfile>>(Model.FullPath!);
        if (profiles is { Count: > 0 })
            ReplaceProfiles(profiles.Where(p => !string.IsNullOrWhiteSpace(p.Name)));
        if (Model.DataCollection.Count == 0)
        {
            // 默认项来自工厂实际公开的驱动目录，不在 Controls 中复制某个品牌的固定标识。
            var fallback = new CameraProfile
            {
                Name = "相机01",
                DriverId = DefaultDriverId,
            };
            Model.DataCollection.Add(fallback);
            Model.SelectedProfile = fallback;
        }
    }

    private void ReplaceProfiles(IEnumerable<CameraProfile> profiles)
    {
        Model.DataCollection.Clear();
        foreach (var profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.DriverId))
                profile.DriverId = DefaultDriverId;
            Model.DataCollection.Add(profile);
        }
        Model.SelectedProfile = Model.DataCollection.FirstOrDefault();
    }

    /// <summary>
    /// 获取工厂目录中的第一个驱动作为新档案和旧空配置的默认值。
    /// 目录顺序由工厂稳定维护，Controls 不需要知道任何具体品牌标识。
    /// </summary>
    private string DefaultDriverId => Model.Drivers[0].Value;
}
