using System.Runtime.InteropServices;
using Basler.Pylon;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Drivers.Cameras;

namespace Device.Drivers.Cameras.Basler;

/// <summary>
/// 直接基于 Basler Pylon SDK 的 GigE 相机适配器。
/// </summary>
internal sealed class BaslerGigeCamera : Device.Core.Cameras.ICamera
{
    private readonly object _captureGate = new();
    private Camera? _camera;
    private TaskCompletionSource<ImageFrame>? _pendingCapture;

    /// <summary>当前生效的触发配置；连接时取创建配置，运行时可由 SetTriggerAsync 改写。</summary>
    private CameraTriggerSettings _frameTrigger;
    private CameraTriggerSettings _lineTrigger;

    public BaslerGigeCamera(CameraOptions options)
    {
        Options = options;
        _frameTrigger = options.FrameTrigger;
        _lineTrigger = options.LineTrigger;
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
            var cameraInfo = CameraFinder.Enumerate()
                .FirstOrDefault(x => string.Equals(
                    x[CameraInfoKey.DeviceIpAddress],
                    Options.IpAddress,
                    StringComparison.OrdinalIgnoreCase));

            if (cameraInfo is null)
                throw new InvalidOperationException($"未发现 IP 地址为 '{Options.IpAddress}' 的 Basler GigE 相机。");

            _camera = new Camera(cameraInfo);
            _camera.ConnectionLost += OnConnectionLost;
            _camera.StreamGrabber.ImageGrabbed += OnImageGrabbed;
            _camera.Open();

            LoadParameterFile();
            ApplyExposure(Options.ExposureMilliseconds);
            ApplyGain(Options.Gain);
            ConfigureTriggers();

            if (!_camera.Parameters[PLCamera.AcquisitionMode]
                    .TrySetValue(PLCamera.AcquisitionMode.Continuous))
            {
                throw new InvalidOperationException("设置 Basler 连续采集模式失败。");
            }

            _camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            State = DeviceState.Connected;
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            TryReleaseAfterConnectFailure();
            return ValueTask.FromResult(
                DeviceResult.Failure("camera.basler.connect_failed", exception.Message));
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
                DeviceResult.Failure("camera.basler.disconnect_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    public async ValueTask<ImageFrame> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var camera = _camera;
        if (State != DeviceState.Connected || camera is null)
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
                camera.ExecuteSoftwareTrigger();

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
        var camera = _camera;
        if (State != DeviceState.Connected || camera is null)
            return ValueTask.FromResult(DeviceResult.Failure(
                "camera.basler.not_connected", $"相机 '{Name}' 尚未连接。"));

        try
        {
            ApplyExposure(exposureMilliseconds);
            ApplyGain(gain);
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(DeviceResult.Failure("camera.basler.param_failed", exception.Message));
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
                "camera.basler.not_connected", $"相机 '{Name}' 尚未连接。"));

        var invalid = CameraDriverConfiguration.ValidateTriggerSettings(Options.ScanType, frameTrigger, lineTrigger);
        if (invalid is not null)
            return ValueTask.FromResult(DeviceResult.Failure("camera.basler.trigger_invalid", invalid));

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
            return ValueTask.FromResult(DeviceResult.Failure("camera.basler.trigger_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private void ApplyExposure(double exposureMilliseconds)
    {
        var camera = GetConnectedCamera();
        var exposureMicroseconds = exposureMilliseconds * 1_000d;

        if (camera.Parameters.Contains(PLCamera.ExposureTime))
        {
            SetFloatParameter(camera.Parameters[PLCamera.ExposureTime], exposureMicroseconds, "曝光时间");
            return;
        }

        if (camera.Parameters.Contains(PLCamera.ExposureTimeAbs))
        {
            SetFloatParameter(camera.Parameters[PLCamera.ExposureTimeAbs], exposureMicroseconds, "曝光时间");
            return;
        }

        throw new InvalidOperationException("Basler 相机不支持可写的曝光时间参数。");
    }

    private void ApplyGain(double gain)
    {
        var camera = GetConnectedCamera();

        if (camera.Parameters.Contains(PLCamera.Gain))
        {
            SetFloatParameter(camera.Parameters[PLCamera.Gain], gain, "增益");
            return;
        }

        if (camera.Parameters.Contains(PLCamera.GainAbs))
        {
            SetFloatParameter(camera.Parameters[PLCamera.GainAbs], gain, "增益");
            return;
        }

        if (camera.Parameters.Contains(PLCamera.GainRaw))
        {
            var parameter = camera.Parameters[PLCamera.GainRaw];
            if (!parameter.IsReadable || !parameter.IsWritable)
                throw new InvalidOperationException("Basler 相机增益参数不可写。");

            parameter.SetValue(Convert.ToInt64(gain));
            return;
        }

        throw new InvalidOperationException("Basler 相机不支持可写的增益参数。");
    }

    private void LoadParameterFile()
    {
        if (Options.ParameterFilePath is null)
            return;

        GetConnectedCamera().Parameters.Load(Options.ParameterFilePath, ParameterPath.CameraDevice);
    }

    /// <summary>连接期下发触发配置（用创建相机时的那一组）。</summary>
    private void ConfigureTriggers() => ApplyTriggers(_frameTrigger, _lineTrigger);

    /// <summary>下发一组触发配置；连接期与运行时改触发共用此实现。</summary>
    private void ApplyTriggers(CameraTriggerSettings frameTrigger, CameraTriggerSettings lineTrigger)
    {
        var camera = GetConnectedCamera();
        foreach (var binding in CameraDriverConfiguration.GetTriggerBindings(Options.ScanType, frameTrigger, lineTrigger))
        {
            if (binding.Selector is not null
                && !camera.Parameters[PLCamera.TriggerSelector].TrySetValue(binding.Selector))
            {
                throw new InvalidOperationException($"设置 Basler 触发选择器 '{binding.Selector}' 失败。");
            }

            var mode = CameraDriverConfiguration.ToGenICamMode(binding.Settings.Mode);
            if (!camera.Parameters[PLCamera.TriggerMode].TrySetValue(mode))
                throw new InvalidOperationException($"设置 Basler {binding.Selector ?? "当前"}触发模式失败。");

            if (binding.Settings.Mode == CameraTriggerMode.On
                && !camera.Parameters[PLCamera.TriggerSource].TrySetValue(binding.Settings.Source))
            {
                throw new InvalidOperationException(
                    $"设置 Basler {binding.Selector ?? "当前"}触发源 '{binding.Settings.Source}' 失败。");
            }
        }
    }

    private void OnImageGrabbed(object? sender, ImageGrabbedEventArgs eventArgs)
    {
        TaskCompletionSource<ImageFrame>? completion;
        lock (_captureGate)
            completion = _pendingCapture;

        if (completion is null)
            return;

        try
        {
            var grabResult = eventArgs.GrabResult;
            if (!grabResult.IsValid)
            {
                completion.TrySetException(new InvalidOperationException("Basler SDK 返回了无效图像帧。"));
                return;
            }

            var length = checked((int)grabResult.PayloadSize);
            var buffer = new byte[length];
            Marshal.Copy(grabResult.PixelDataPointer, buffer, 0, length);

            completion.TrySetResult(new ImageFrame(
                grabResult.Width,
                grabResult.Height,
                grabResult.PixelTypeValue.ToString(),
                buffer));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private void OnConnectionLost(object? sender, EventArgs eventArgs)
    {
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

        var camera = _camera;
        _camera = null;
        if (camera is null)
            return;

        camera.ConnectionLost -= OnConnectionLost;
        camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed;

        if (camera.StreamGrabber.IsGrabbing)
            camera.StreamGrabber.Stop();

        if (camera.IsOpen)
            camera.Close();

        camera.Dispose();
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

    private Camera GetConnectedCamera() =>
        _camera ?? throw new InvalidOperationException("Basler 相机尚未初始化。");

    private static void SetFloatParameter(IFloatParameter parameter, double value, string parameterName)
    {
        if (!parameter.IsReadable || !parameter.IsWritable)
            throw new InvalidOperationException($"Basler 相机{parameterName}参数不可写。");

        parameter.SetValue(value);
    }
}
