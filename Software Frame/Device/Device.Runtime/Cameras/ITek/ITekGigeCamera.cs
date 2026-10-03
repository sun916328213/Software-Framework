// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：ITekGigeCamera。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Runtime.InteropServices、System.Text、Device.Core.Cameras、Device.Core.Devices、Device.Runtime.Cameras、IKapC.NET。导入不代表所有类型都会在运行时加载。
using System.Runtime.InteropServices;
using System.Text;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;
using IKapC.NET;

namespace Device.Runtime.Cameras.ITek;

/// <summary>
/// 直接基于 IKapC SDK 的 I-Tek GigE 相机适配器。
/// </summary>
internal sealed class ITekGigeCamera : ICamera
{
    private static readonly object EnvironmentGate = new();
    private static int _environmentUsers;

    private readonly object _captureGate = new();
    private readonly List<IntPtr> _buffers = [];
    private readonly IKapCLib.PITKSTREAMCALLBACK _endFrameCallback;
    private readonly IKapCLib.PITKEVENTINFOCALLBACK _deviceRemovedCallback;
    private TaskCompletionSource<ImageFrame>? _pendingCapture;
    private IntPtr _camera = new(-1);
    private IntPtr _stream = new(-1);
    private IntPtr _bufferData = new(-1);
    private int _currentBufferIndex;
    private int _bufferCapacity;
    private long _width;
    private long _height;
    private string _pixelFormat = "Unknown";
    private bool _environmentAcquired;
    private bool _streamStarted;

    /// <summary>当前生效的触发配置；连接时取创建配置，运行时可由 SetTriggerAsync 改写。</summary>
    private CameraTriggerSettings _frameTrigger;
    private CameraTriggerSettings _lineTrigger;

    /// <summary>
    /// 初始化 ITekGigeCamera 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public ITekGigeCamera(CameraOptions options)
    {
        Options = options;
        _frameTrigger = options.FrameTrigger;
        _lineTrigger = options.LineTrigger;
        _endFrameCallback = OnEndOfFrame;
        _deviceRemovedCallback = OnDeviceRemoved;
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
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Ok、AcquireEnvironment、FindDeviceIndex；具体条件和先后次序见方法体。
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
            AcquireEnvironment();
            _environmentAcquired = true;

            var deviceIndex = FindDeviceIndex(Options.IpAddress);
            EnsureSuccess(
                IKapCLib.ItkDevOpen(
                    deviceIndex,
                    (int)ItkDeviceAccessMode.ITKDEV_VAL_ACCESS_MODE_EXCLUSIVE,
                    ref _camera),
                "打开相机");

            EnsureSuccess(
                IKapCLib.ItkDevRegisterCallback(_camera, "DeviceRemove", _deviceRemovedCallback, _camera),
                "注册掉线回调");

            LoadParameterFile();
            ConfigureExposureAndGain(Options.ExposureMilliseconds, Options.Gain);
            ConfigureTriggers();
            CreateStreamAndBuffers();
            StartStream();

            State = DeviceState.Connected;
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            State = DeviceState.Faulted;
            TryReleaseAfterConnectFailure();
            return ValueTask.FromResult(
                DeviceResult.Failure("camera.itek.connect_failed", exception.Message));
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
                DeviceResult.Failure("camera.itek.disconnect_failed", exception.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 CaptureAsync。
    /// 正文中的关键协作调用包括 ShouldExecuteSoftwareTrigger、EnsureSuccess、ItkDevExecuteCommand、CreateLinkedTokenSource、ConfigureAwait；具体条件和先后次序见方法体。
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
                EnsureSuccess(IKapCLib.ItkDevExecuteCommand(_camera, "TriggerSoftware"), "执行软件触发");

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
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Failure、ConfigureExposureAndGain、Ok；具体条件和先后次序见方法体。
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
                "camera.itek.not_connected", $"相机 '{Name}' 尚未连接。"));

        try
        {
            ConfigureExposureAndGain(exposureMilliseconds, gain);
            return ValueTask.FromResult(DeviceResult.Ok());
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(DeviceResult.Failure("camera.itek.param_failed", exception.Message));
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
                "camera.itek.not_connected", $"相机 '{Name}' 尚未连接。"));

        var invalid = CameraDriverConfiguration.ValidateTriggerSettings(Options.ScanType, frameTrigger, lineTrigger);
        if (invalid is not null)
            return ValueTask.FromResult(DeviceResult.Failure("camera.itek.trigger_invalid", invalid));

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
            return ValueTask.FromResult(DeviceResult.Failure("camera.itek.trigger_failed", exception.Message));
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
    /// 处理当前类型提供的操作；对应入口为 FindDeviceIndex。
    /// 正文中的关键协作调用包括 EnsureSuccess、ItkManGetDeviceCount、ItkManGetDeviceInfo、Equals、ItkManGetGigEDeviceInfo；具体条件和先后次序见方法体。
    /// </summary>
    private static uint FindDeviceIndex(string ipAddress)
    {
        uint deviceCount = 0;
        EnsureSuccess(IKapCLib.ItkManGetDeviceCount(ref deviceCount), "枚举 I-Tek 相机");

        for (uint index = 0; index < deviceCount; index++)
        {
            var deviceInfo = new IKapCLib.ITKDEV_INFO();
            EnsureSuccess(IKapCLib.ItkManGetDeviceInfo(index, ref deviceInfo), "读取相机信息");
            if (!string.Equals(deviceInfo.DeviceClass, "GigEVision", StringComparison.OrdinalIgnoreCase))
                continue;

            var gigeInfo = new IKapCLib.ITKGIGEDEV_INFO();
            EnsureSuccess(IKapCLib.ItkManGetGigEDeviceInfo(index, ref gigeInfo), "读取 GigE 相机信息");
            if (string.Equals(gigeInfo.Ip, ipAddress, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        throw new InvalidOperationException($"未发现 IP 地址为 '{ipAddress}' 的 I-Tek GigE 相机。");
    }

    /// <summary>
    /// 配置当前对象或组合入口；对应入口为 ConfigureExposureAndGain。
    /// 正文中的关键协作调用包括 EnsureSuccess、ItkDevSetInt64、ItkDevSetDouble；具体条件和先后次序见方法体。
    /// </summary>
    private void ConfigureExposureAndGain(int exposureMilliseconds, double gain)
    {
        EnsureSuccess(
            IKapCLib.ItkDevSetInt64(_camera, "ExposureTime", exposureMilliseconds * 1_000L),
            "设置曝光时间");
        EnsureSuccess(IKapCLib.ItkDevSetDouble(_camera, "DigitalGain", gain), "设置相机增益");
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadParameterFile。
    /// 正文中的关键协作调用包括 EnsureSuccess、ItkDevLoadConfigurationFromFile；具体条件和先后次序见方法体。
    /// </summary>
    private void LoadParameterFile()
    {
        if (Options.ParameterFilePath is null)
            return;

        EnsureSuccess(
            IKapCLib.ItkDevLoadConfigurationFromFile(_camera, Options.ParameterFilePath),
            "加载 I-Tek 相机参数文件");
    }

    /// <summary>连接期下发触发配置（用创建相机时的那一组）。</summary>
    private void ConfigureTriggers() => ApplyTriggers(_frameTrigger, _lineTrigger);

    /// <summary>下发一组触发配置；连接期与运行时改触发共用此实现。</summary>
    private void ApplyTriggers(CameraTriggerSettings frameTrigger, CameraTriggerSettings lineTrigger)
    {
        foreach (var binding in CameraDriverConfiguration.GetTriggerBindings(Options.ScanType, frameTrigger, lineTrigger))
        {
            if (binding.Selector is not null)
                SetStringFeature("TriggerSelector", binding.Selector);

            SetStringFeature(
                "TriggerMode",
                CameraDriverConfiguration.ToGenICamMode(binding.Settings.Mode));

            if (binding.Settings.Mode == CameraTriggerMode.On)
                SetStringFeature("TriggerSource", binding.Settings.Source);
        }
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateStreamAndBuffers。
    /// 正文中的关键协作调用包括 EnsureSuccess、ItkDevGetStreamCount、ItkDevGetInt64、ItkDevToString、ToString；具体条件和先后次序见方法体。
    /// </summary>
    private void CreateStreamAndBuffers()
    {
        uint streamCount = 0;
        EnsureSuccess(IKapCLib.ItkDevGetStreamCount(_camera, ref streamCount), "读取数据流数量");
        if (streamCount == 0)
            throw new InvalidOperationException("I-Tek 相机没有可用的数据流通道。");

        EnsureSuccess(IKapCLib.ItkDevGetInt64(_camera, "Width", ref _width), "读取图像宽度");
        EnsureSuccess(IKapCLib.ItkDevGetInt64(_camera, "Height", ref _height), "读取图像高度");

        var pixelFormat = new StringBuilder(128);
        uint pixelFormatLength = 128;
        EnsureSuccess(
            IKapCLib.ItkDevToString(_camera, "PixelFormat", pixelFormat, ref pixelFormatLength),
            "读取像素格式");
        _pixelFormat = pixelFormat.ToString();
        var bufferFormat = MapBufferFormat(_pixelFormat);

        var firstBuffer = IntPtr.Zero;
        EnsureSuccess(IKapCLib.ItkBufferNew(_width, _height, bufferFormat, ref firstBuffer), "创建图像缓冲区");
        _buffers.Add(firstBuffer);

        EnsureSuccess(IKapCLib.ItkDevAllocStream(_camera, 0, firstBuffer, ref _stream), "创建相机数据流");

        const int bufferCount = 5;
        for (var index = 1; index < bufferCount; index++)
        {
            var buffer = IntPtr.Zero;
            EnsureSuccess(IKapCLib.ItkBufferNew(_width, _height, bufferFormat, ref buffer), "创建图像缓冲区");
            // 先记录句柄，保证后续添加到数据流失败时仍能在统一回滚路径中释放。
            _buffers.Add(buffer);
            EnsureSuccess(IKapCLib.ItkStreamAddBuffer(_stream, buffer), "添加图像缓冲区");
        }

        _bufferCapacity = ReadBufferSize(firstBuffer);
        _bufferData = Marshal.AllocHGlobal(_bufferCapacity);
        if (_bufferData == IntPtr.Zero)
            throw new OutOfMemoryException("无法为 I-Tek 图像帧分配托管复制缓冲区。");
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StartStream。
    /// 正文中的关键协作调用包括 SetStreamParameter、EnsureSuccess、ItkStreamRegisterCallback、ItkStreamStart；具体条件和先后次序见方法体。
    /// </summary>
    private void StartStream()
    {
        SetStreamParameter(
            ItkStreamPrm.ITKSTREAM_PRM_START_MODE,
            (int)ItkStreamStartMode.ITKSTREAM_VAL_START_MODE_NON_BLOCK);
        SetStreamParameter(
            ItkStreamPrm.ITKSTREAM_PRM_TRANSFER_MODE,
            (int)ItkStreamTransferMode.ITKSTREAM_VAL_TRANSFER_MODE_SYNCHRONOUS_WITH_PROTECT);
        SetStreamParameter(ItkStreamPrm.ITKSTREAM_PRM_TIME_OUT, Options.TimeoutMilliseconds);

        EnsureSuccess(
            IKapCLib.ItkStreamRegisterCallback(
                _stream,
                (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_FRAME,
                _endFrameCallback,
                _stream),
            "注册帧结束回调");

        _currentBufferIndex = 0;
        EnsureSuccess(IKapCLib.ItkStreamStart(_stream, IKapCLib.ITKSTREAM_CONTINUOUS), "启动相机数据流");
        _streamStarted = true;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnEndOfFrame。
    /// 正文中的关键协作调用包括 ReadBufferSize、EnsureSuccess、ItkBufferRead、Copy、TrySetResult；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void OnEndOfFrame(uint eventType, IntPtr context)
    {
        TaskCompletionSource<ImageFrame>? completion;
        lock (_captureGate)
            completion = _pendingCapture;

        try
        {
            var buffer = _buffers[_currentBufferIndex];
            var size = ReadBufferSize(buffer);
            if (size > _bufferCapacity)
                throw new InvalidOperationException("I-Tek 图像帧超过已分配缓冲区大小。");

            EnsureSuccess(IKapCLib.ItkBufferRead(buffer, 0, _bufferData, (uint)size), "读取图像缓冲区");
            if (completion is not null)
            {
                var managedBuffer = new byte[size];
                Marshal.Copy(_bufferData, managedBuffer, 0, size);
                completion.TrySetResult(new ImageFrame(
                    checked((int)_width),
                    checked((int)_height),
                    _pixelFormat,
                    managedBuffer));
            }
        }
        catch (Exception exception)
        {
            completion?.TrySetException(exception);
        }
        finally
        {
            if (_buffers.Count > 0)
                _currentBufferIndex = (_currentBufferIndex + 1) % _buffers.Count;
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnDeviceRemoved。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    private void OnDeviceRemoved(IntPtr context, IntPtr eventInfo)
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
    /// 正文中的关键协作调用包括 IsValidHandle、ItkStreamStop、ItkStreamUnregisterCallback、ItkStreamRemoveBuffer、ItkDevFreeStream；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    private void ReleaseSdkResources()
    {
        lock (_captureGate)
        {
            _pendingCapture?.TrySetCanceled();
            _pendingCapture = null;
        }

        if (_streamStarted && IsValidHandle(_stream))
        {
            IKapCLib.ItkStreamStop(_stream);
            _streamStarted = false;
        }

        if (IsValidHandle(_stream))
        {
            IKapCLib.ItkStreamUnregisterCallback(
                _stream,
                (uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_FRAME);

            foreach (var buffer in _buffers)
                IKapCLib.ItkStreamRemoveBuffer(_stream, buffer);

            IKapCLib.ItkDevFreeStream(_stream);
            _stream = new IntPtr(-1);
        }

        // 缓冲区可能在数据流创建前或添加过程中失败，因此不能依赖有效的数据流句柄释放。
        foreach (var buffer in _buffers)
            IKapCLib.ItkBufferFree(buffer);

        _buffers.Clear();
        _currentBufferIndex = 0;
        _bufferCapacity = 0;

        if (IsValidHandle(_bufferData))
        {
            Marshal.FreeHGlobal(_bufferData);
            _bufferData = new IntPtr(-1);
        }

        if (IsValidHandle(_camera))
        {
            IKapCLib.ItkDevUnregisterCallback(_camera, "DeviceRemove");
            IKapCLib.ItkDevClose(_camera);
            _camera = new IntPtr(-1);
        }

        if (_environmentAcquired)
        {
            ReleaseEnvironment();
            _environmentAcquired = false;
        }
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
    /// 读取当前实现提供的数据；对应入口为 ReadBufferSize。
    /// 正文中的关键协作调用包括 AllocHGlobal、EnsureSuccess、ItkBufferGetPrm、ReadInt64、FreeHGlobal；具体条件和先后次序见方法体。
    /// </summary>
    private static int ReadBufferSize(IntPtr buffer)
    {
        var sizePointer = Marshal.AllocHGlobal(sizeof(long));
        try
        {
            EnsureSuccess(
                IKapCLib.ItkBufferGetPrm(buffer, (uint)ItkBufferPrm.ITKBUFFER_PRM_SIZE, sizePointer),
                "读取图像缓冲区大小");
            return checked((int)Marshal.ReadInt64(sizePointer));
        }
        finally
        {
            Marshal.FreeHGlobal(sizePointer);
        }
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetStringFeature。
    /// 正文中的关键协作调用包括 EnsureSuccess、ItkDevFromString；具体条件和先后次序见方法体。
    /// </summary>
    private void SetStringFeature(string name, string value)
    {
        EnsureSuccess(IKapCLib.ItkDevFromString(_camera, name, value), $"设置参数 {name}");
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetStreamParameter。
    /// 正文中的关键协作调用包括 AllocHGlobal、WriteInt32、EnsureSuccess、ItkStreamSetPrm、FreeHGlobal；具体条件和先后次序见方法体。
    /// </summary>
    private void SetStreamParameter(ItkStreamPrm parameter, int value)
    {
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(pointer, value);
            EnsureSuccess(IKapCLib.ItkStreamSetPrm(_stream, (uint)parameter, pointer), $"设置数据流参数 {parameter}");
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 MapBufferFormat。
    /// </summary>
    private static uint MapBufferFormat(string pixelFormat) => pixelFormat switch
    {
        "Mono8" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO8,
        "Mono10" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO10,
        "Mono10Packed" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO10PACKED,
        "Mono12" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO12,
        "Mono12Packed" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO12PACKED,
        "Mono14" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO14,
        "Mono16" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO16,
        "BayerGR8" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR8,
        "BayerRG8" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG8,
        "BayerGB8" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB8,
        "BayerBG8" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG8,
        "RGB8" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB888,
        "BGR8" => (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR888,
        _ => throw new NotSupportedException($"I-Tek 像素格式 '{pixelFormat}' 暂不支持。"),
    };

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 AcquireEnvironment。
    /// 正文中的关键协作调用包括 EnsureSuccess、ItkManInitialize；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    internal static void AcquireEnvironment()
    {
        lock (EnvironmentGate)
        {
            if (_environmentUsers == 0)
                EnsureSuccess(IKapCLib.ItkManInitialize(), "初始化 IKapC 环境");

            _environmentUsers++;
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseEnvironment。
    /// 正文中的关键协作调用包括 ItkManTerminate；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
    internal static void ReleaseEnvironment()
    {
        lock (EnvironmentGate)
        {
            if (_environmentUsers == 0)
                return;

            _environmentUsers--;
            if (_environmentUsers == 0)
                IKapCLib.ItkManTerminate();
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 IsValidHandle。
    /// </summary>
    private static bool IsValidHandle(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EnsureSuccess。
    /// </summary>
    private static void EnsureSuccess(uint resultCode, string operation)
    {
        if (resultCode != (uint)ItkStatusErrorId.ITKSTATUS_OK)
            throw new InvalidOperationException($"{operation}失败，SDK 错误码：0x{resultCode:X8}。");
    }
}
