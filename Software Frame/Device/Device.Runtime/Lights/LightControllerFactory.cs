// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LightControllerFactory、Registration。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.Lights、Device.Runtime.Lights.Cst、Device.Runtime.Lights.Opt。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.Lights;
using Device.Runtime.Lights.Cst;
using Device.Runtime.Lights.Opt;

namespace Device.Runtime.Lights;

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
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 Validate、TryGetValue、create、Join、Select；具体条件和先后次序见方法体。
    /// </remarks>
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

    /// <summary>
    /// Registration 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
    /// </summary>
    private sealed record Registration(
        DeviceDriverDescriptor Descriptor,
        Func<LightControllerOptions, ILightController> Create);
}
