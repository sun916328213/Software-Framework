// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FakeCameraFactory、FakeCamera、TestFrames。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、Device.Workbench.Tests、Device.Core.Cameras、Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using Device.Workbench.Tests;
using Device.Core.Cameras;
using Device.Core.Devices;

namespace Device.Workbench.Tests.Fakes;

/// <summary>可记录创建参数与实例序列的假相机工厂（不碰硬件/厂商 SDK）。</summary>
public sealed class FakeCameraFactory : ICameraFactory
{
    /// <summary>
    /// 初始化 FakeCameraFactory 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 ToArray、Select；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Create；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
    /// </summary>
    public ICamera Create(CameraOptions options)
    {
        CreatedOptions.Add(options);
        var camera = new FakeCamera(options);
        Cameras.Add(camera);
        return camera;
    }

    /// <summary>
    /// 测试相关入口 EnumerateDevicesAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromResult、Failure；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ConnectAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromResult、Failure、Ok；具体条件和先后次序见方法体。
    /// </summary>
    public ValueTask<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connectCount++;
        if (FailConnect)
            return ValueTask.FromResult(DeviceResult.Failure("connect.failed", "测试连接失败"));

        State = DeviceState.Connected;
        return ValueTask.FromResult(DeviceResult.Ok());
    }

    /// <summary>
    /// 测试相关入口 DisconnectAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromResult、Ok；具体条件和先后次序见方法体。
    /// </summary>
    public ValueTask<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = DeviceState.Disconnected;
        return ValueTask.FromResult(DeviceResult.Ok());
    }

    /// <summary>
    /// 测试相关入口 CaptureAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromResult、Mono8；具体条件和先后次序见方法体。
    /// </summary>
    public ValueTask<ImageFrame> CaptureAsync(CancellationToken cancellationToken = default)
    {
        _captureCount++;
        if (FailCapture)
            throw new InvalidOperationException("测试抓图失败");
        if (State != DeviceState.Connected)
            throw new InvalidOperationException("相机未连接");
        return ValueTask.FromResult(NextFrame ?? TestFrames.Mono8(4, 4));
    }

    /// <summary>
    /// 测试相关入口 SetExposureGainAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromResult、Ok；具体条件和先后次序见方法体。
    /// </summary>
    public ValueTask<DeviceResult> SetExposureGainAsync(
        int exposureMilliseconds,
        double gain,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(DeviceResult.Ok());

    /// <summary>
    /// 测试相关入口 SetTriggerAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、FromResult、Ok；具体条件和先后次序见方法体。
    /// </summary>
    public ValueTask<DeviceResult> SetTriggerAsync(
        CameraTriggerSettings frameTrigger,
        CameraTriggerSettings lineTrigger,
        CancellationToken cancellationToken = default)
    {
        AppliedTriggers.Add((frameTrigger, lineTrigger));
        return ValueTask.FromResult(DeviceResult.Ok());
    }

    /// <summary>
    /// 测试相关入口 DisposeAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
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
    /// <summary>
    /// 测试相关入口 Mono8；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToArray、Select；具体条件和先后次序见方法体。
    /// </summary>
    public static ImageFrame Mono8(int width, int height, byte fill = 0x40) =>
        new(width, height, "Mono8", new byte[width * height].Select(b => fill).ToArray());

    /// <summary>
    /// 测试相关入口 Mono16；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public static ImageFrame Mono16(int width, int height) =>
        new(width, height, "Mono16", new byte[width * height * 2]);

    /// <summary>
    /// 测试相关入口 Bgr24；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public static ImageFrame Bgr24(int width, int height) =>
        new(width, height, "BGR8", new byte[width * height * 3]);
}
