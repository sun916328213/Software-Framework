using System.Runtime.InteropServices;
using Device.Core.Cameras;
using Device.Core.Devices;
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

    public DahuaGigeCamera(CameraOptions options)
    {
        Options = options;
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
            ApplyExposureAndGain(Options.ExposureMilliseconds, Options.Gain);
            ConfigureSoftwareTrigger();
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

    private void ConfigureSoftwareTrigger()
    {
        EnsureSuccess(_camera.IMV_SetEnumFeatureSymbol("TriggerMode", "On"), "开启触发模式");
        EnsureSuccess(_camera.IMV_SetEnumFeatureSymbol("TriggerSource", "Software"), "设置软件触发源");
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
