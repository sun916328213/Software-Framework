// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：ProductionDeviceSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、Device.Core.Cameras、Device.Core.Devices、Device.Core.Lights、Device.Core.Rfid、Device.Core.Scanners。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Core.Lights;
using Device.Core.Rfid;
using Device.Core.Scanners;
using Device.Controls.Models;
using Extension.Core.Conversion;
using System.IO;

namespace MachineApp.Infra.Device;

/// <summary>持有本机型配置的生产设备；生产会话与界面工作区各自拥有设备，不能互相代替。</summary>
/// <remarks>
/// ProductionDeviceSession 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：IProductionDevices。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class ProductionDeviceSession(
    ICameraFactory cameras, ILightControllerFactory lights, IRfidDeviceFactory rfid, IScannerFactory scanners)
    : IProductionDevices
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly List<IDevice> _owned = [];
    private bool _disposed;
    public IReadOnlyList<ICamera> Cameras { get; private set; } = [];
    public IReadOnlyList<ILightController> Lights { get; private set; } = [];
    public IReadOnlyList<IRfidDevice> Rfid { get; private set; } = [];
    public IReadOnlyList<IScanner> Scanners { get; private set; } = [];

    /// <summary>
    /// 执行初始化步骤；对应入口为 InitializeAsync。
    /// 正文中的关键协作调用包括 InitializeAsync、Combine；具体条件和先后次序见方法体。
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        InitializeAsync(Path.Combine(AppContext.BaseDirectory, "Config"), cancellationToken);

    /// <summary>
    /// 执行初始化步骤；对应入口为 InitializeAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ThrowIf、Load、ToArray；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public async Task InitializeAsync(string configDirectory, CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_owned.Count != 0) throw new InvalidOperationException("生产设备已经初始化。");
            // 先验证全部配置文件，再接触真实硬件；缺失或无效配置直接失败，不静默切换到模拟设备。
            var cameraProfiles = Load<CameraProfile>(configDirectory, "Cameras.json");
            var lightProfiles = Load<LightProfile>(configDirectory, "Lights.json");
            var rfidProfiles = Load<RfidProfile>(configDirectory, "Rfids.json");
            var scannerProfiles = Load<ScannerProfile>(configDirectory, "Scanners.json");
            try
            {
                Cameras = cameraProfiles.Select(profile => Own(cameras.Create(profile.ToOptions()))).ToArray();
                Lights = lightProfiles.Select(profile => Own(lights.Create(profile.ToOptions()))).ToArray();
                Rfid = rfidProfiles.Select(profile => Own(rfid.Create(profile.ToOptions()))).ToArray();
                Scanners = scannerProfiles.Select(profile => Own(scanners.Create(profile.ToOptions()))).ToArray();
                foreach (var device in _owned)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Ensure(await device.ConnectAsync(cancellationToken).ConfigureAwait(false));
                }
                foreach (var light in Lights)
                    Ensure(await light.SetAllIntensityAsync(0, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception primary)
            {
                var cleanup = await ReleaseAllAsync().ConfigureAwait(false);
                if (cleanup.Count > 0) throw new AggregateException(new[] { primary }.Concat(cleanup));
                throw;
            }
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Camera。
    /// 正文中的关键协作调用包括 FirstOrDefault、Equals；具体条件和先后次序见方法体。
    /// </summary>
    public ICamera Camera(string name) => Cameras.FirstOrDefault(camera => string.Equals(camera.Name, name, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"未配置生产相机：{name}");

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Ensure。
    /// </summary>
    public static void Ensure(DeviceResult result)
    {
        if (!result.Success) throw new InvalidOperationException($"{result.ErrorCode}: {result.Message}");
    }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、ReleaseAllAsync、Release；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            var errors = await ReleaseAllAsync().ConfigureAwait(false);
            if (errors.Count > 0) throw new AggregateException("部分生产设备释放失败。", errors);
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Own。
    /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
    /// </summary>
    private T Own<T>(T device) where T : IDevice { _owned.Add(device); return device; }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseAllAsync。
    /// 正文中的关键协作调用包括 Reverse、AsEnumerable、Ensure、ConfigureAwait、SetAllIntensityAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task<List<Exception>> ReleaseAllAsync()
    {
        var errors = new List<Exception>();
        foreach (var device in _owned.AsEnumerable().Reverse())
        {
            try
            {
                if (device is ILightController light && light.State == DeviceState.Connected)
                    Ensure(await light.SetAllIntensityAsync(0).ConfigureAwait(false));
            }
            catch (Exception error) { errors.Add(error); }
            try { await device.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
        }
        _owned.Clear();
        Cameras = []; Lights = []; Rfid = []; Scanners = [];
        return errors;
    }

    /// <summary>
    /// 执行加载操作；对应入口为 Load。
    /// 正文中的关键协作调用包括 Combine、Exists、Read、Any、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    private static List<T> Load<T>(string directory, string fileName) where T : IDeviceProfile
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException("请先在新设备配置页完成生产设备档案。", path);
        var profiles = JsonEx.Read<List<T>>(path);
        if (profiles is not { Count: > 0 }) throw new InvalidDataException($"生产设备档案为空：{path}");
        if (profiles.Any(profile => string.IsNullOrWhiteSpace(profile.Name)) ||
            profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count() != profiles.Count)
            throw new InvalidDataException($"生产设备名称为空或重复：{path}");
        return profiles;
    }
}
