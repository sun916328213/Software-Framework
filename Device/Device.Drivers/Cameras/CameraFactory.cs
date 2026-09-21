using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Drivers.Cameras.Basler;
using Device.Drivers.Cameras.Dahua;
using Device.Drivers.Cameras.Hik;
using Device.Drivers.Cameras.ITek;

namespace Device.Drivers.Cameras;

/// <summary>
/// 所有相机品牌共用的工厂。
/// </summary>
/// <remarks>
/// 本类是内置相机驱动的唯一注册点。新增品牌时只需在此增加一条注册，
/// 不需要在 Core 中同步维护驱动标识清单，也不增加品牌专用工厂类型。
/// 构造工厂时仅保存这些委托，不会创建任何相机实例，也不会连接或枚举真实硬件。
/// 相机实例的创建时机由调用方按实际业务流程决定。
/// </remarks>
public sealed class CameraFactory : ICameraFactory
{
    private readonly Registration[] _registrations;

    /// <summary>
    /// 创建包含当前已实现相机驱动注册信息的工厂；此过程不会创建相机实例。
    /// </summary>
    public CameraFactory()
    {
        var registrations = new[]
        {
            // 驱动标识与适配器绑定只在这里声明一次。下列 Lambda 仅保存延迟创建逻辑，
            // 只有调用方使用对应 DriverId 调用 Create 时才会实例化相机。
            CreateRegistration(
                "camera.hik.gige",
                "Hikvision",
                "GigE",
                "海康 GigE 相机",
                options => new HikGigeCamera(options),
                HikCameraEnumerator.Enumerate),
            CreateRegistration(
                "camera.basler.gige",
                "Basler",
                "GigE",
                "Basler GigE 相机",
                options => new BaslerGigeCamera(options),
                BaslerCameraEnumerator.Enumerate),
            CreateRegistration(
                "camera.dahua.gige",
                "Dahua",
                "GigE",
                "大华 GigE 相机",
                options => new DahuaGigeCamera(options),
                DahuaCameraEnumerator.Enumerate),
            CreateRegistration(
                "camera.itek.gige",
                "I-Tek",
                "GigE",
                "I-Tek GigE 相机",
                options => new ITekGigeCamera(options),
                ITekCameraEnumerator.Enumerate),
        };

        AvailableDrivers = registrations
            .Select(x => x.Descriptor)
            .OrderBy(x => x.Brand, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Model, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _registrations = registrations;
    }

    /// <inheritdoc />
    public IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    /// <inheritdoc />
    public ICamera Create(CameraOptions options)
    {
        CameraOptionsValidator.Validate(options);

        foreach (var registration in _registrations)
        {
            if (registration.Descriptor.Id == options.DriverId)
            {
                // 仅执行当前 DriverId 命中的一个创建委托，不会实例化其他品牌相机。
                return registration.CreateCamera(options);
            }
        }

        var installedDrivers = string.Join(", ", AvailableDrivers.Select(x => x.Id));
        throw new CameraDriverNotFoundException(options.DriverId, installedDrivers);
    }

    /// <summary>
    /// 把一条驱动声明组装成内部注册项，并把同一个稳定标识传给设备枚举器。
    /// </summary>
    /// <remarks>
    /// 枚举器不再自行保存驱动标识，因此创建路由与枚举结果不会出现两份配置不同步的问题。
    /// </remarks>
    private static Registration CreateRegistration(
        string driverId,
        string brand,
        string model,
        string displayName,
        Func<CameraOptions, ICamera> createCamera,
        Func<DeviceDriverId, IReadOnlyList<CameraDeviceDescriptor>> enumerate)
    {
        var id = new DeviceDriverId(driverId);
        return new Registration(
            new DeviceDriverDescriptor(id, DeviceCategory.Camera, brand, model, displayName),
            createCamera,
            () => enumerate(id));
    }

    /// <inheritdoc />
    public async ValueTask<DeviceResult<IReadOnlyList<CameraDeviceDescriptor>>> EnumerateDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 各家 SDK 枚举都是阻塞式 P/Invoke，统一放到线程池执行。
        return await Task.Run(() => EnumerateDevices(cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private DeviceResult<IReadOnlyList<CameraDeviceDescriptor>> EnumerateDevices(
        CancellationToken cancellationToken)
    {
        var devices = new List<CameraDeviceDescriptor>();
        var errors = new List<string>();

        foreach (var registration in _registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                devices.AddRange(registration.Enumerate());
            }
            catch (Exception exception)
            {
                // 目标机器可能未安装某个品牌的 SDK，单个驱动失败不影响其余结果。
                errors.Add($"{registration.Descriptor.Brand}: {exception.Message}");
            }
        }

        IReadOnlyList<CameraDeviceDescriptor> value = devices
            .OrderBy(x => x.Brand, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (errors.Count == 0)
            return DeviceResult<IReadOnlyList<CameraDeviceDescriptor>>.Ok(value);
        if (value.Count > 0)
            return new DeviceResult<IReadOnlyList<CameraDeviceDescriptor>>(
                true, value, "camera.enumerate_partial", string.Join("; ", errors));
        return DeviceResult<IReadOnlyList<CameraDeviceDescriptor>>.Failure(
            "camera.enumerate_failed", string.Join("; ", errors));
    }

    private sealed record Registration(
        DeviceDriverDescriptor Descriptor,
        Func<CameraOptions, ICamera> CreateCamera,
        Func<IReadOnlyList<CameraDeviceDescriptor>> Enumerate);
}
