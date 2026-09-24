using System.Runtime.InteropServices;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Drivers.Cameras;
using MVSDK_Net;

namespace Device.Drivers.Cameras.Dahua;

/// <summary>
/// 直接基于大华 MV SDK 的 GigE 相机适配器。
/// </summary>
internal sealed class DahuaGigeCamera : ICamera
{
    private readonly object _captureGate = new();
    private readonly MyCamera _camera = new();
    private readonly IMVDefine.IMV_FrameCallBack _frameCallback;
    private readonly IMVDefine.IMV_ConnectCallBack _connectionCallback;
    private TaskCompletionSource<ImageFrame>? _pendingCapture;
    private bool _handleCreated;
    private bool _grabbing;

    /// <summary>当前生效的触发配置；连接时取创建配置，运行时可由 SetTriggerAsync 改写。</summary>
    private CameraTriggerSettings _frameTrigger;
    private CameraTriggerSettings _lineTrigger;

    public DahuaGigeCamera(CameraOptions options)
    {
        Options = options;
        _frameTrigger = options.FrameTrigger;
        _lineTrigger = options.LineTrigger;
        _frameCallback = OnFrameReceived;
        _connectionCallback = OnConnectionChanged;
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
            EnsureSuccess(
                _camera.IMV_CreateHandle(
                    IMVDefine.IMV_ECreateHandleMode.modeByIPAddress,
                    0,
                    Options.IpAddress),
                "创建相机句柄");
            _handleCreated = true;

            EnsureSuccess(_camera.IMV_Open(), "打开相机");
            EnsureSuccess(_camera.IMV_SetBufferCount(8), "设置采集缓冲区数量");
            EnsureSuccess(_camera.IMV_SubscribeConnectArg(_connectionCallback, IntPtr.Zero), "注册连接状态回调");
            EnsureSuccess(_camera.IMV_AttachGrabbing(_frameCallback, IntPtr.Zero), "注册图像回调");
            LoadParameterFile();
            ApplyExposureAndGain(Options.ExposureMilliseconds, Options.Gain);
            ConfigureTriggers();
            EnsureSuccess(_camera.IMV_StartGrabbing(), "启动相机采集");
            _grabbing = true;

            State = DeviceState.Connected;
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            TryReleaseAfterConnectFailure();
            return ValueTask.FromResult(
                DeviceResult.Failure("camera.dahua.connect_failed", exception.Message));
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
            return ValueTask.FromResult(
                DeviceResult.Failure("camera.dahua.disconnect_failed", exception.Message));
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
                EnsureSuccess(_camera.IMV_ExecuteCommandFeature("TriggerSoftware"), "执行软件触发");

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
                "camera.dahua.not_connected", $"相机 '{Name}' 尚未连接。"));

        try
        {
            ApplyExposureAndGain(exposureMilliseconds, gain);
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(DeviceResult.Failure("camera.dahua.param_failed", exception.Message));
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
                "camera.dahua.not_connected", $"相机 '{Name}' 尚未连接。"));

        var invalid = CameraDriverConfiguration.ValidateTriggerSettings(Options.ScanType, frameTrigger, lineTrigger);
        if (invalid is not null)
            return ValueTask.FromResult(DeviceResult.Failure("camera.dahua.trigger_invalid", invalid));

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
            return ValueTask.FromResult(DeviceResult.Failure("camera.dahua.trigger_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private void ApplyExposureAndGain(int exposureMilliseconds, double gain)
    {
        EnsureSuccess(
            _camera.IMV_SetDoubleFeatureValue("ExposureTime", exposureMilliseconds * 1_000d),
            "设置曝光时间");
        EnsureSuccess(_camera.IMV_SetDoubleFeatureValue("Gain", gain), "设置相机增益");
    }

    private void LoadParameterFile()
    {
        if (Options.ParameterFilePath is null)
            return;

        var errors = new IMVDefine.IMV_ErrorList();
        EnsureSuccess(
            _camera.IMV_LoadDeviceCfg(Options.ParameterFilePath, ref errors),
            "加载大华相机参数文件");
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
                    _camera.IMV_SetEnumFeatureSymbol("TriggerSelector", binding.Selector),
                    $"设置大华触发选择器 '{binding.Selector}'");
            }

            EnsureSuccess(
                _camera.IMV_SetEnumFeatureSymbol(
                    "TriggerMode",
                    CameraDriverConfiguration.ToGenICamMode(binding.Settings.Mode)),
                $"设置大华 {binding.Selector ?? "当前"}触发模式");

            if (binding.Settings.Mode == CameraTriggerMode.On)
            {
                EnsureSuccess(
                    _camera.IMV_SetEnumFeatureSymbol("TriggerSource", binding.Settings.Source),
                    $"设置大华 {binding.Selector ?? "当前"}触发源 '{binding.Settings.Source}'");
            }
        }
    }

    private void OnFrameReceived(ref IMVDefine.IMV_Frame frame, IntPtr userData)
    {
        TaskCompletionSource<ImageFrame>? completion;
        lock (_captureGate)
            completion = _pendingCapture;

        if (completion is null)
            return;

        try
        {
            var length = checked((int)frame.frameInfo.size);
            var buffer = new byte[length];
            Marshal.Copy(frame.pData, buffer, 0, length);

            completion.TrySetResult(new ImageFrame(
                checked((int)frame.frameInfo.width),
                checked((int)frame.frameInfo.height),
                "Native",
                buffer));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private void OnConnectionChanged(ref IMVDefine.IMV_SConnectArg connection, IntPtr userData)
    {
        if (connection.EvType != IMVDefine.IMV_EVType.offLine)
            return;

        State = DeviceState.Faulted;
        lock (_captureGate)
        {
            _pendingCapture?.TrySetException(new InvalidOperationException($"相机 '{Name}' 连接已断开。"));
            _pendingCapture = null;
        }
    }

    private void ReleaseSdkResources()
    {
        lock (_captureGate)
        {
            _pendingCapture?.TrySetCanceled();
            _pendingCapture = null;
        }

        // 创建句柄失败时不能继续调用依赖有效句柄的 SDK 查询函数。
        if (_handleCreated && (_grabbing || _camera.IMV_IsGrabbing()))
        {
            _camera.IMV_StopGrabbing();
            _grabbing = false;
        }

        if (!_handleCreated)
            return;

        _camera.IMV_Close();
        _camera.IMV_DestroyHandle();
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
        if (resultCode != IMVDefine.IMV_OK)
            throw new InvalidOperationException($"{operation}失败，SDK 错误码：0x{resultCode:X8}。");
    }
}
