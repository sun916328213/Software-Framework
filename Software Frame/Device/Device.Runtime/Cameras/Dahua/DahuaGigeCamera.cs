// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：DahuaGigeCamera。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Runtime.InteropServices、Device.Core.Cameras、Device.Core.Devices、Device.Runtime.Cameras、MVSDK_Net。导入不代表所有类型都会在运行时加载。
using System.Runtime.InteropServices;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;
using MVSDK_Net;

namespace Device.Runtime.Cameras.Dahua;

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

    /// <summary>
    /// 初始化 DahuaGigeCamera 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Ok、EnsureSuccess、IMV_CreateHandle；具体条件和先后次序见方法体。
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
                DeviceResult.Failure("camera.dahua.disconnect_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 CaptureAsync。
    /// 正文中的关键协作调用包括 ShouldExecuteSoftwareTrigger、EnsureSuccess、IMV_ExecuteCommandFeature、CreateLinkedTokenSource、ConfigureAwait；具体条件和先后次序见方法体。
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
    /// 处理当前类型提供的操作；对应入口为 ApplyExposureAndGain。
    /// 正文中的关键协作调用包括 EnsureSuccess、IMV_SetDoubleFeatureValue；具体条件和先后次序见方法体。
    /// </summary>
    private void ApplyExposureAndGain(int exposureMilliseconds, double gain)
    {
        EnsureSuccess(
            _camera.IMV_SetDoubleFeatureValue("ExposureTime", exposureMilliseconds * 1_000d),
            "设置曝光时间");
        EnsureSuccess(_camera.IMV_SetDoubleFeatureValue("Gain", gain), "设置相机增益");
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadParameterFile。
    /// 正文中的关键协作调用包括 EnsureSuccess、IMV_LoadDeviceCfg；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnFrameReceived。
    /// 正文中的关键协作调用包括 Copy、TrySetResult、TrySetException；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnConnectionChanged。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseSdkResources。
    /// 正文中的关键协作调用包括 IMV_IsGrabbing、IMV_StopGrabbing、IMV_Close、IMV_DestroyHandle；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EnsureSuccess。
    /// </summary>
    private static void EnsureSuccess(int resultCode, string operation)
    {
        if (resultCode != IMVDefine.IMV_OK)
            throw new InvalidOperationException($"{operation}失败，SDK 错误码：0x{resultCode:X8}。");
    }
}
