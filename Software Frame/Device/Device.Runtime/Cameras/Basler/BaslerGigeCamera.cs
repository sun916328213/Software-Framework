// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：BaslerGigeCamera。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Runtime.InteropServices、Basler.Pylon、Device.Core.Cameras、Device.Core.Devices、Device.Runtime.Cameras。导入不代表所有类型都会在运行时加载。
using System.Runtime.InteropServices;
using Basler.Pylon;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;

namespace Device.Runtime.Cameras.Basler;

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

    /// <summary>
    /// 初始化 BaslerGigeCamera 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Ok、FirstOrDefault、Enumerate；具体条件和先后次序见方法体。
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
            return ValueTask.FromResult(
                DeviceResult.Failure("camera.basler.disconnect_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 CaptureAsync。
    /// 正文中的关键协作调用包括 ShouldExecuteSoftwareTrigger、ExecuteSoftwareTrigger、CreateLinkedTokenSource、ConfigureAwait、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 设置当前实现管理的值或状态；对应入口为 SetExposureGainAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Failure、ApplyExposure、ApplyGain；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// 处理当前类型提供的操作；对应入口为 ApplyExposure。
    /// 正文中的关键协作调用包括 GetConnectedCamera、Contains、SetFloatParameter；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ApplyGain。
    /// 正文中的关键协作调用包括 GetConnectedCamera、Contains、SetFloatParameter、SetValue、ToInt64；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 执行加载操作；对应入口为 LoadParameterFile。
    /// 正文中的关键协作调用包括 Load、GetConnectedCamera；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnImageGrabbed。
    /// 正文中的关键协作调用包括 TrySetException、Copy、TrySetResult、ToString；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnConnectionLost。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    private void OnConnectionLost(object? sender, EventArgs eventArgs)
    {
        State = DeviceState.Faulted;
        lock (_captureGate)
        {
            _pendingCapture?.TrySetException(new InvalidOperationException($"相机 '{Name}' 连接已断开。"));
            _pendingCapture = null;
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseSdkResources。
    /// 正文中的关键协作调用包括 Stop、Close、Dispose；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
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

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetConnectedCamera。
    /// </summary>
    private Camera GetConnectedCamera() =>
        _camera ?? throw new InvalidOperationException("Basler 相机尚未初始化。");

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetFloatParameter。
    /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
    /// </summary>
    private static void SetFloatParameter(IFloatParameter parameter, double value, string parameterName)
    {
        if (!parameter.IsReadable || !parameter.IsWritable)
            throw new InvalidOperationException($"Basler 相机{parameterName}参数不可写。");

        parameter.SetValue(value);
    }
}
