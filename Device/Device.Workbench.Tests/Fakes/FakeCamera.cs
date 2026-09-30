using Device.Core.Cameras;
using Device.Core.Devices;

namespace Device.Workbench.Tests.Fakes;

/// <summary>可记录创建参数与实例序列的假相机工厂（不碰硬件/厂商 SDK）。</summary>
public sealed class FakeCameraFactory : ICameraFactory
{
    public FakeCameraFactory(params string[] driverIds)
    {
        AvailableDrivers = driverIds
            .Select(id => new DeviceDriverDescriptor(
                new DeviceDriverId(id),
                DeviceCategory.Camera,
                "Fake",
                "Fake",
                id))
            .ToArray();
    }

    public IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    public List<CameraOptions> CreatedOptions { get; } = [];

    public List<FakeCamera> Cameras { get; } = [];

    public int CreateCallCount => CreatedOptions.Count;

    public ICamera Create(CameraOptions options)
    {
        CreatedOptions.Add(options);
        var camera = new FakeCamera(options);
        Cameras.Add(camera);
        return camera;
    }

    public ValueTask<DeviceResult<IReadOnlyList<CameraDeviceDescriptor>>> EnumerateDevicesAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(
            DeviceResult<IReadOnlyList<CameraDeviceDescriptor>>.Failure(
                "unsupported",
                "测试工厂不支持枚举。"));
}

/// <summary>内存假相机：连接状态、抓图帧、触发下发均可断言。</summary>
public sealed class FakeCamera(CameraOptions options) : ICamera
{
    private int _connectCount;
    private int _captureCount;
    private int _disposeCount;

    public CameraOptions Options { get; } = options;

    public string Name => Options.Name;

    public DeviceState State { get; private set; } = DeviceState.Disconnected;

    public int ConnectCount => _connectCount;

    public int CaptureCount => _captureCount;

    public int DisposeCount => _disposeCount;

    public bool FailConnect { get; set; }

    public bool FailCapture { get; set; }

    public ImageFrame? NextFrame { get; set; } = TestFrames.Mono8(4, 4);

    public List<(CameraTriggerSettings Frame, CameraTriggerSettings Line)> AppliedTriggers { get; } = [];

    public ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connectCount++;
        if (FailConnect)
            return ValueTask.FromResult(DeviceResult.Failure("connect.failed", "测试连接失败"));

        State = DeviceState.Connected;
        return ValueTask.FromResult(DeviceResult.Ok());
    }

    public ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = DeviceState.Disconnected;
        return ValueTask.FromResult(DeviceResult.Ok());
    }

    public ValueTask<ImageFrame> CaptureAsync(CancellationToken cancellationToken = default)
    {
        _captureCount++;
        if (FailCapture)
            throw new InvalidOperationException("测试抓图失败");
        if (State != DeviceState.Connected)
            throw new InvalidOperationException("相机未连接");
        return ValueTask.FromResult(NextFrame ?? TestFrames.Mono8(4, 4));
    }

    public ValueTask<DeviceResult> SetExposureGainAsync(
        int exposureMilliseconds,
        double gain,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(DeviceResult.Ok());

    public ValueTask<DeviceResult> SetTriggerAsync(
        CameraTriggerSettings frameTrigger,
        CameraTriggerSettings lineTrigger,
        CancellationToken cancellationToken = default)
    {
        AppliedTriggers.Add((frameTrigger, lineTrigger));
        return ValueTask.FromResult(DeviceResult.Ok());
    }

    public ValueTask DisposeAsync()
    {
        _disposeCount++;
        State = DeviceState.Disconnected;
        return ValueTask.CompletedTask;
    }
}

/// <summary>固定像素内容的测试帧。</summary>
public static class TestFrames
{
    public static ImageFrame Mono8(int width, int height, byte fill = 0x40) =>
        new(width, height, "Mono8", new byte[width * height].Select(b => fill).ToArray());

    public static ImageFrame Mono16(int width, int height) =>
        new(width, height, "Mono16", new byte[width * height * 2]);

    public static ImageFrame Bgr24(int width, int height) =>
        new(width, height, "BGR8", new byte[width * height * 3]);
}
