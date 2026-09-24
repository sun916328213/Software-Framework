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
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

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

    private void ConfigureNetworkPacket(CCameraInfo cameraInfo)
    {
        if (cameraInfo.nTLayerType != CSystem.MV_GIGE_DEVICE)
            return;

        var packetSize = _camera.GIGE_GetOptimalPacketSize();
        if (packetSize > 0)
            EnsureSuccess(_camera.SetIntValue("GevSCPSPacketSize", (uint)packetSize), "设置 GigE 网络包大小");
    }

    private void ApplyExposureAndGain(int exposureMilliseconds, double gain)
    {
        EnsureSuccess(_camera.SetEnumValue("ExposureAuto", 0), "关闭自动曝光");
        EnsureSuccess(
            _camera.SetFloatValue("ExposureTime", Convert.ToSingle(exposureMilliseconds * 1_000d)),
            "设置曝光时间");

        EnsureSuccess(_camera.SetEnumValue("GainAuto", 0), "关闭自动增益");
        EnsureSuccess(_camera.SetFloatValue("Gain", Convert.ToSingle(gain)), "设置相机增益");
    }

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

    private static void EnsureSuccess(int resultCode, string operation)
    {
        if (resultCode != CErrorDefine.MV_OK)
            throw new InvalidOperationException($"{operation}失败，SDK 错误码：0x{resultCode:X8}。");
    }
}
