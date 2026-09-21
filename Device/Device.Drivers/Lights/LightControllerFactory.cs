using Device.Core.Devices;
using Device.Core.Lights;
using Device.Drivers.Lights.Cst;
using Device.Drivers.Lights.Opt;

namespace Device.Drivers.Lights;

/// <summary>
/// 所有光源控制器品牌共用的工厂。
/// </summary>
/// <remarks>
/// 本类是内置光源控制器驱动的唯一注册点。新增品牌时只需在此增加一条注册，
/// 调用方不需要品牌枚举或品牌专用工厂。
/// </remarks>
public sealed class LightControllerFactory : ILightControllerFactory
{
    private readonly IReadOnlyDictionary<DeviceDriverId, Func<LightControllerOptions, ILightController>> _creators;

    /// <summary>创建包含 CST 和 OPT 串口控制器的工厂。</summary>
    public LightControllerFactory()
    {
        var registrations = new[]
        {
            CreateRegistration(
                "light.cst.serial",
                "CST",
                "Serial",
                "CST 串口光源控制器",
                options => new CstLightController(options)),
            CreateRegistration(
                "light.opt.serial",
                "OPT",
                "Serial",
                "OPT 串口光源控制器",
                options => new OptLightController(options)),
        };

        AvailableDrivers = registrations
            .Select(x => x.Descriptor)
            .OrderBy(x => x.Brand, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Model, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _creators = registrations.ToDictionary(x => x.Descriptor.Id, x => x.Create);
    }

    /// <inheritdoc />
    public IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    /// <inheritdoc />
    public ILightController Create(LightControllerOptions options)
    {
        LightControllerOptionsValidator.Validate(options);

        if (_creators.TryGetValue(options.DriverId, out var create))
            return create(options);

        var installedDrivers = string.Join(", ", AvailableDrivers.Select(x => x.Id));
        throw new LightControllerDriverNotFoundException(options.DriverId, installedDrivers);
    }

    /// <summary>把驱动描述和延迟创建委托组装为唯一注册项。</summary>
    private static Registration CreateRegistration(
        string driverId,
        string brand,
        string model,
        string displayName,
        Func<LightControllerOptions, ILightController> create)
    {
        var descriptor = new DeviceDriverDescriptor(
            new DeviceDriverId(driverId),
            DeviceCategory.LightController,
            brand,
            model,
            displayName);
        return new Registration(descriptor, create);
    }

    private sealed record Registration(
        DeviceDriverDescriptor Descriptor,
        Func<LightControllerOptions, ILightController> Create);
}
