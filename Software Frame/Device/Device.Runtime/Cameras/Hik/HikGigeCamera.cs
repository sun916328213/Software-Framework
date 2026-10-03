// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：HikGigeCamera。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Runtime.InteropServices、Device.Core.Cameras、Device.Core.Devices、Device.Runtime.Cameras、MvCamCtrl.NET、MvCamCtrl.NET.CameraParams。导入不代表所有类型都会在运行时加载。
using System.Runtime.InteropServices;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;
using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;

namespace Device.Runtime.Cameras.Hik;

/// <summary>
/// 直接基于海康 MVS SDK 的 GigE 相机适配器，不依赖旧 Device 项目。
/// </summary>
internal sealed class HikGigeCamera : ICamera
{
    private readonly object _captureGate = new();
    private readonly CCamera _camera = new();
    private readonly cbOutputExdelegate _imageCallback;
    private TaskCompletionSource<ImageFrame>? _pendingCapture;
    private bool _handleCreated;
    private bool _grabbing;

    /// <summary>当前生效的触发配置；连接时取创建配置，运行时可由 SetTriggerAsync 改写。</summary>
    private CameraTriggerSettings _frameTrigger;
    private CameraTriggerSettings _lineTrigger;

    /// <summary>
    /// 初始化 HikGigeCamera 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public HikGigeCamera(CameraOptions options)
    {
        Options = options;
        _frameTrigger = options.FrameTrigger;
        _lineTrigger = options.LineTrigger;
        _imageCallback = OnImageReceived;
    }

    /// <inheritdoc />
    public string Name => Options.Name;

    /// <inheritdoc />
    public DeviceState State { get; private set; } = DeviceState.Disconnected;

    /// <inheritdoc />
    public CameraOptions Options { get; }

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Ok、FindCameraByIp、EnsureSuccess；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (State == DeviceState.Connected)
            return ValueTask.FromResult(DeviceResult.Ok());

        State = DeviceState.Connecting;
        try
        {
            var cameraInfo = FindCameraByIp(Options.IpAddress);
            EnsureSuccess(_camera.CreateHandle(ref cameraInfo), "创建相机句柄");
            _handleCreated = true;

            EnsureSuccess(_camera.OpenDevice(), "打开相机");
            ConfigureNetworkPacket(cameraInfo);
            LoadParameterFile();
            ApplyExposureAndGain(Options.ExposureMilliseconds, Options.Gain);
            ConfigureTriggers();
            EnsureSuccess(_camera.RegisterImageCallBackEx(_imageCallback, IntPtr.Zero), "注册图像回调");
            EnsureSuccess(_camera.StartGrabbing(), "启动相机采集");
            _grabbing = true;

            State = DeviceState.Connected;
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            TryReleaseAfterConnectFailure();
            return ValueTask.FromResult(DeviceResult.Failure("camera.hik.connect_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、ReleaseSdkResources、FromResult、Ok、Failure；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            ReleaseSdkResources();
            State = DeviceState.Disconnected;
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            return ValueTask.FromResult(DeviceResult.Failure("camera.hik.disconnect_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 CaptureAsync。
    /// 正文中的关键协作调用包括 ShouldExecuteSoftwareTrigger、EnsureSuccess、SetCommandValue、CreateLinkedTokenSource、ConfigureAwait；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public async ValueTask<ImageFrame> CaptureAsync(CancellationToken cancellationToken = default)
    {
        if (State != DeviceState.Connected)
            throw new InvalidOperationException($"相机 '{Name}' 尚未连接。");

        TaskCompletionSource<ImageFrame> completion;
        lock (_captureGate)
        {
            if (_pendingCapture is not null)
                throw new InvalidOperationException($"相机 '{Name}' 已有正在进行的采集操作。");

            completion = new TaskCompletionSource<ImageFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingCapture = completion;
        }

        try
        {
            if (CameraDriverConfiguration.ShouldExecuteSoftwareTrigger(_frameTrigger))
                EnsureSuccess(_camera.SetCommandValue("TriggerSoftware"), "执行软件触发");

            using var timeout = new CancellationTokenSource(Options.TimeoutMilliseconds);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            try
            {
                return await completion.Task.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"相机 '{Name}' 在 {Options.TimeoutMilliseconds} 毫秒内未返回图像。");
            }
        }
        finally
        {
            lock (_captureGate)
            {
                if (ReferenceEquals(_pendingCapture, completion))
                    _pendingCapture = null;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 设置当前实现管理的值或状态；对应入口为 SetExposureGainAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Failure、ApplyExposureAndGain、Ok；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public ValueTask<DeviceResult> SetExposureGainAsync(
        int exposureMilliseconds,
        double gain,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (State != DeviceState.Connected)
            return ValueTask.FromResult(DeviceResult.Failure(
                "camera.hik.not_connected", $"相机 '{Name}' 尚未连接。"));

        try
        {
            ApplyExposureAndGain(exposureMilliseconds, gain);
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(DeviceResult.Failure("camera.hik.param_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 设置当前实现管理的值或状态；对应入口为 SetTriggerAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Failure、ValidateTriggerSettings、ApplyTriggers；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public ValueTask<DeviceResult> SetTriggerAsync(
        CameraTriggerSettings frameTrigger,
        CameraTriggerSettings lineTrigger,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (State != DeviceState.Connected)
            return ValueTask.FromResult(DeviceResult.Failure(
                "camera.hik.not_connected", $"相机 '{Name}' 尚未连接。"));

        var invalid = CameraDriverConfiguration.ValidateTriggerSettings(Options.ScanType, frameTrigger, lineTrigger);
        if (invalid is not null)
            return ValueTask.FromResult(DeviceResult.Failure("camera.hik.trigger_invalid", invalid));

        try
        {
            // 下发成功后才记住新配置：失败时 CaptureAsync 仍按上一次生效的帧触发判断是否发软件触发。
            ApplyTriggers(frameTrigger, lineTrigger);
            _frameTrigger = frameTrigger;
            _lineTrigger = lineTrigger;
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(DeviceResult.Failure("camera.hik.trigger_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、DisconnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 FindCameraByIp。
    /// 正文中的关键协作调用包括 EnsureSuccess、EnumDevices、Join、Equals；具体条件和先后次序见方法体。
    /// </summary>
    private static CCameraInfo FindCameraByIp(string ipAddress)
    {
        var devices = new List<CCameraInfo>();
        EnsureSuccess(CSystem.EnumDevices(CSystem.MV_GIGE_DEVICE, ref devices), "枚举 GigE 相机");

        foreach (var device in devices)
        {
            if (device is not CGigECameraInfo gigeInfo)
                continue;

            var currentIp = string.Join(
                '.',
                (gigeInfo.nCurrentIp & 0xff000000) >> 24,
                (gigeInfo.nCurrentIp & 0x00ff0000) >> 16,
                (gigeInfo.nCurrentIp & 0x0000ff00) >> 8,
                gigeInfo.nCurrentIp & 0x000000ff);

            if (string.Equals(currentIp, ipAddress, StringComparison.OrdinalIgnoreCase))
                return device;
        }

        throw new InvalidOperationException($"未发现 IP 地址为 '{ipAddress}' 的海康 GigE 相机。");
    }

    /// <summary>
    /// 配置当前对象或组合入口；对应入口为 ConfigureNetworkPacket。
    /// 正文中的关键协作调用包括 GIGE_GetOptimalPacketSize、EnsureSuccess、SetIntValue；具体条件和先后次序见方法体。
    /// </summary>
    private void ConfigureNetworkPacket(CCameraInfo cameraInfo)
    {
        if (cameraInfo.nTLayerType != CSystem.MV_GIGE_DEVICE)
            return;

        var packetSize = _camera.GIGE_GetOptimalPacketSize();
        if (packetSize > 0)
            EnsureSuccess(_camera.SetIntValue("GevSCPSPacketSize", (uint)packetSize), "设置 GigE 网络包大小");
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ApplyExposureAndGain。
    /// 正文中的关键协作调用包括 EnsureSuccess、SetEnumValue、SetFloatValue、ToSingle；具体条件和先后次序见方法体。
    /// </summary>
    private void ApplyExposureAndGain(int exposureMilliseconds, double gain)
    {
        EnsureSuccess(_camera.SetEnumValue("ExposureAuto", 0), "关闭自动曝光");
        EnsureSuccess(
            _camera.SetFloatValue("ExposureTime", Convert.ToSingle(exposureMilliseconds * 1_000d)),
            "设置曝光时间");

        EnsureSuccess(_camera.SetEnumValue("GainAuto", 0), "关闭自动增益");
        EnsureSuccess(_camera.SetFloatValue("Gain", Convert.ToSingle(gain)), "设置相机增益");
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadParameterFile。
    /// 正文中的关键协作调用包括 EnsureSuccess、FeatureLoad；具体条件和先后次序见方法体。
    /// </summary>
    private void LoadParameterFile()
    {
        if (Options.ParameterFilePath is null)
            return;

        EnsureSuccess(_camera.FeatureLoad(Options.ParameterFilePath), "加载海康相机参数文件");
    }

    /// <summary>连接期下发触发配置（用创建相机时的那一组）。</summary>
    private void ConfigureTriggers() => ApplyTriggers(_frameTrigger, _lineTrigger);

    /// <summary>下发一组触发配置；连接期与运行时改触发共用此实现。</summary>
    private void ApplyTriggers(CameraTriggerSettings frameTrigger, CameraTriggerSettings lineTrigger)
    {
        foreach (var binding in CameraDriverConfiguration.GetTriggerBindings(Options.ScanType, frameTrigger, lineTrigger))
        {
            if (binding.Selector is not null)
            {
                EnsureSuccess(
                    _camera.SetEnumValueByString("TriggerSelector", binding.Selector),
                    $"设置海康触发选择器 '{binding.Selector}'");
            }

            EnsureSuccess(
                _camera.SetEnumValueByString(
                    "TriggerMode",
                    CameraDriverConfiguration.ToGenICamMode(binding.Settings.Mode)),
                $"设置海康 {binding.Selector ?? "当前"}触发模式");

            if (binding.Settings.Mode == CameraTriggerMode.On)
            {
                EnsureSuccess(
                    _camera.SetEnumValueByString("TriggerSource", binding.Settings.Source),
                    $"设置海康 {binding.Selector ?? "当前"}触发源 '{binding.Settings.Source}'");
            }
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnImageReceived。
    /// 正文中的关键协作调用包括 Copy、TrySetResult、ToString、TrySetException；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void OnImageReceived(IntPtr data, ref MV_FRAME_OUT_INFO_EX frameInfo, IntPtr userData)
    {
        TaskCompletionSource<ImageFrame>? completion;
        lock (_captureGate)
            completion = _pendingCapture;

        if (completion is null)
            return;

        try
        {
            var length = checked((int)frameInfo.nFrameLen);
            var buffer = new byte[length];
            Marshal.Copy(data, buffer, 0, length);

            completion.TrySetResult(new ImageFrame(
                frameInfo.nWidth,
                frameInfo.nHeight,
                frameInfo.enPixelType.ToString(),
                buffer));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseSdkResources。
    /// 正文中的关键协作调用包括 StopGrabbing、CloseDevice、DestroyHandle；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    private void ReleaseSdkResources()
    {
        lock (_captureGate)
        {
            _pendingCapture?.TrySetCanceled();
            _pendingCapture = null;
        }

        if (_grabbing)
        {
            _camera.StopGrabbing();
            _grabbing = false;
        }

        if (!_handleCreated)
            return;

        _camera.CloseDevice();
        _camera.DestroyHandle();
        _handleCreated = false;
    }

    /// <summary>连接失败后的尽力清理不能覆盖最初的 SDK 错误。</summary>
    private void TryReleaseAfterConnectFailure()
    {
        try
        {
            ReleaseSdkResources();
        }
        catch
        {
            // 对外保留最初的连接失败信息；调用方仍可再次执行 DisconnectAsync 尝试清理。
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EnsureSuccess。
    /// </summary>
    private static void EnsureSuccess(int resultCode, string operation)
    {
        if (resultCode != CErrorDefine.MV_OK)
            throw new InvalidOperationException($"{operation}失败，SDK 错误码：0x{resultCode:X8}。");
    }
}
