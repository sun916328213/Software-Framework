// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ProductionDeviceSessionTests、Config、Factory、FakeDevice、Camera、Light、Rfid、Scanner。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、Device.Controls.Models、Device.Core.Cameras、Device.Core.Devices、Device.Core.Lights、Device.Core.Rfid。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using Device.Controls.Models;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Core.Lights;
using Device.Core.Rfid;
using Device.Core.Scanners;
using Extension.Core.Conversion;
using MachineApp.Infra.Device;
using System.IO;

namespace Device.Workbench.Tests;

/// <summary>
/// ProductionDeviceSessionTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ProductionDeviceSessionTests
{
    /// <summary>
    /// 验证 PersistedStartupFlowUsesTheVerifiedNewDeviceScript 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、ReadAllText、Combine、GetString、GetProperty；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PersistedStartupFlowUsesTheVerifiedNewDeviceScript()
    {
        using var project = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "StartupFlow.taskproj")));
        var code = project.RootElement.GetProperty("Scripts")[2].GetProperty("Code").GetString();
        Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InitializeDevicesScript.txt")).Replace("\r\n", "\n"), code!.Replace("\r\n", "\n"));
        Assert.Contains("ProductionDeviceSession", code);
        Assert.DoesNotContain("CamManagerService", code);
    }

    /// <summary>
    /// 验证 CreatesAllProfilesAndReleasesEveryDeviceOnce 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Empty、InitializeAsync、Equal、Camera、Single；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CreatesAllProfilesAndReleasesEveryDeviceOnce()
    {
        using var config = new Config();
        var factory = new Factory();
        var session = new ProductionDeviceSession(factory, factory, factory, factory);
        Assert.Empty(factory.Created);
        await session.InitializeAsync(config.Directory);
        Assert.Equal(2, session.Cameras.Count);
        Assert.Equal("camera2", session.Camera("camera2").Name);
        Assert.Single(session.Lights);
        Assert.Single(session.Rfid);
        Assert.Single(session.Scanners);
        Assert.All(factory.Created, device => Assert.Equal(DeviceState.Connected, device.State));
        Assert.Equal(1, factory.Created.OfType<Light>().Single().OffCalls);
        await session.DisposeAsync();
        await session.DisposeAsync();
        Assert.All(factory.Created, device => Assert.Equal(1, device.DisposeCalls));
        Assert.Equal(2, factory.Created.OfType<Light>().Single().OffCalls);
        Assert.Empty(session.Cameras);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.InitializeAsync(config.Directory));
    }

    /// <summary>
    /// 验证 MissingOrDuplicateProfilesNeverCreateDevices 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Delete、Combine、ThrowsAsync、InitializeAsync、Empty；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task MissingOrDuplicateProfilesNeverCreateDevices()
    {
        using var config = new Config();
        var factory = new Factory();
        await using var session = new ProductionDeviceSession(factory, factory, factory, factory);
        File.Delete(Path.Combine(config.Directory, "Rfids.json"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => session.InitializeAsync(config.Directory));
        Assert.Empty(factory.Created);
        config.Write();
        JsonEx.Save(new[] { new CameraProfile { Name = "same", DriverId = "test" }, new CameraProfile { Name = "same", DriverId = "test" } },
            Path.Combine(config.Directory, "Cameras.json"));
        await Assert.ThrowsAsync<InvalidDataException>(() => session.InitializeAsync(config.Directory));
        Assert.Empty(factory.Created);
    }

    /// <summary>
    /// 验证 FailedConnectionRollsBackAllCreatedDevicesAndAllowsRetry 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、InitializeAsync、All、Equal、Empty；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task FailedConnectionRollsBackAllCreatedDevicesAndAllowsRetry()
    {
        using var config = new Config();
        var factory = new Factory { FailName = "rfid" };
        await using var session = new ProductionDeviceSession(factory, factory, factory, factory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.InitializeAsync(config.Directory));
        Assert.All(factory.Created, device => Assert.Equal(1, device.DisposeCalls));
        Assert.Empty(session.Cameras);
        Assert.False(factory.Created.Single(device => device.Name == "scanner").ConnectCalled);
        factory.FailName = null;
        await session.InitializeAsync(config.Directory);
        Assert.Equal(2, session.Cameras.Count);
    }

    /// <summary>
    /// 验证 CancellationRollsBackEvenDevicesThatWereNotConnected 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Cancel、ThrowsAnyAsync、InitializeAsync、All、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CancellationRollsBackEvenDevicesThatWereNotConnected()
    {
        using var config = new Config();
        using var cancellation = new CancellationTokenSource();
        var factory = new Factory { OnConnect = _ => cancellation.Cancel() };
        await using var session = new ProductionDeviceSession(factory, factory, factory, factory);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.InitializeAsync(config.Directory, cancellation.Token));
        Assert.All(factory.Created, device => Assert.Equal(1, device.DisposeCalls));
        Assert.Empty(session.Cameras);
    }

    /// <summary>
    /// 验证 CleanupFailureDoesNotPreventOtherDeviceReleases 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 InitializeAsync、ThrowsAsync、AsTask、DisposeAsync、Single；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CleanupFailureDoesNotPreventOtherDeviceReleases()
    {
        using var config = new Config();
        var factory = new Factory { DisposeFailName = "scanner" };
        var session = new ProductionDeviceSession(factory, factory, factory, factory);
        await session.InitializeAsync(config.Directory);
        var failure = await Assert.ThrowsAsync<AggregateException>(() => session.DisposeAsync().AsTask());
        Assert.Single(failure.InnerExceptions);
        Assert.All(factory.Created, device => Assert.Equal(1, device.DisposeCalls));
    }

    /// <summary>
    /// Config 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Config : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "ProductionDeviceTests", Guid.NewGuid().ToString("N"));
        /// <summary>
        /// 初始化 Config 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 CreateDirectory、Write；具体条件和先后次序见方法体。
        /// </summary>
        public Config() { System.IO.Directory.CreateDirectory(Directory); Write(); }
        /// <summary>
        /// 验证 Write 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Save、Combine；具体条件和先后次序见方法体。
        /// </summary>
        public void Write()
        {
            JsonEx.Save(new[] { new CameraProfile { Name = "camera1", DriverId = "test" }, new CameraProfile { Name = "camera2", DriverId = "test" } }, Path.Combine(Directory, "Cameras.json"));
            JsonEx.Save(new[] { new LightProfile { Name = "light", DriverId = "test" } }, Path.Combine(Directory, "Lights.json"));
            JsonEx.Save(new[] { new RfidProfile { Name = "rfid", DriverId = "test" } }, Path.Combine(Directory, "Rfids.json"));
            JsonEx.Save(new[] { new ScannerProfile { Name = "scanner", DriverId = "test" } }, Path.Combine(Directory, "Scanners.json"));
        }
        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Delete；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }

    /// <summary>
    /// Factory 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICameraFactory、ILightControllerFactory、IRfidDeviceFactory、IScannerFactory。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Factory : ICameraFactory, ILightControllerFactory, IRfidDeviceFactory, IScannerFactory
    {
        public string? FailName { get; set; }
        public string? DisposeFailName { get; set; }
        public Action<string>? OnConnect { get; set; }
        public List<FakeDevice> Created { get; } = [];
        public IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers => [];
        /// <summary>
        /// 验证 Add 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        private T Add<T>(T device) where T : FakeDevice { device.Factory = this; Created.Add(device); return device; }
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public ICamera Create(CameraOptions options) => Add(new Camera(options));
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public ILightController Create(LightControllerOptions options) => Add(new Light(options));
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public IRfidDevice Create(RfidOptions options) => Add(new Rfid(options));
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public IScanner Create(ScannerOptions options) => Add(new Scanner(options));
        /// <summary>
        /// 验证 EnumerateDevicesAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ValueTask<DeviceResult<IReadOnlyList<CameraDeviceDescriptor>>> EnumerateDevicesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// FakeDevice 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IDevice。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private abstract class FakeDevice(string name) : IDevice
    {
        public Factory Factory { get; set; } = null!;
        public string Name => name;
        public DeviceState State { get; private set; } = DeviceState.Disconnected;
        public int DisposeCalls { get; private set; }
        public bool ConnectCalled { get; private set; }
        /// <summary>
        /// 验证 ConnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Failure、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> ConnectAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); ConnectCalled = true; Factory.OnConnect?.Invoke(Name);
            State = DeviceState.Connected;
            return ValueTask.FromResult(Name == Factory.FailName ? DeviceResult.Failure("connect", Name) : DeviceResult.Ok());
        }
        /// <summary>
        /// 验证 DisconnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> DisconnectAsync(CancellationToken ct = default) { State = DeviceState.Disconnected; return ValueTask.FromResult(DeviceResult.Ok()); }
        /// <summary>
        /// 验证 DisposeAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            DisposeCalls++; State = DeviceState.Disconnected;
            if (Name == Factory.DisposeFailName) throw new IOException(Name);
            return ValueTask.CompletedTask;
        }
    }
    /// <summary>
    /// Camera 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：FakeDevice、ICamera。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Camera(CameraOptions options) : FakeDevice(options.Name), ICamera
    {
        public CameraOptions Options => options;
        /// <summary>
        /// 验证 CaptureAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<ImageFrame> CaptureAsync(CancellationToken ct = default) => ValueTask.FromResult(new ImageFrame(1, 1, "Mono8", new byte[] { 0 }));
        /// <summary>
        /// 验证 SetExposureGainAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> SetExposureGainAsync(int exposure, double gain, CancellationToken ct = default) => ValueTask.FromResult(DeviceResult.Ok());
        /// <summary>
        /// 验证 SetTriggerAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> SetTriggerAsync(CameraTriggerSettings frame, CameraTriggerSettings line, CancellationToken ct = default) => ValueTask.FromResult(DeviceResult.Ok());
    }
    /// <summary>
    /// Light 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：FakeDevice、ILightController。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Light(LightControllerOptions options) : FakeDevice(options.Name), ILightController
    {
        public int OffCalls { get; private set; }
        public LightControllerOptions Options => options;
        /// <summary>
        /// 验证 SetIntensityAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> SetIntensityAsync(int channel, int intensity, CancellationToken ct = default) => ValueTask.FromResult(DeviceResult.Ok());
        /// <summary>
        /// 验证 SetAllIntensityAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、FromResult、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> SetAllIntensityAsync(int intensity, CancellationToken ct = default) { Assert.Equal(0, intensity); OffCalls++; return ValueTask.FromResult(DeviceResult.Ok()); }
    }
    /// <summary>
    /// Rfid 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：FakeDevice、IRfidDevice。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Rfid(RfidOptions options) : FakeDevice(options.Name), IRfidDevice
    {
        public RfidOptions Options => options;
        /// <summary>
        /// 验证 ReadPagesAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult<string>> ReadPagesAsync(int page, int count, CancellationToken ct = default) => ValueTask.FromResult(DeviceResult<string>.Ok("id"));
        /// <summary>
        /// 验证 WritePagesAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> WritePagesAsync(int page, IReadOnlyList<string> pages, CancellationToken ct = default) => ValueTask.FromResult(DeviceResult.Ok());
    }
    /// <summary>
    /// Scanner 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：FakeDevice、IScanner。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Scanner(ScannerOptions options) : FakeDevice(options.Name), IScanner
    {
        public ScannerOptions Options => options;
        public event EventHandler<ScanReceivedEventArgs>? ScanReceived { add { } remove { } }
    }
}
