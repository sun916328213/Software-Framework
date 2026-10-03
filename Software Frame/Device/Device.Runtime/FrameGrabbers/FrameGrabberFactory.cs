// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：FrameGrabberFactory、Registration。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.FrameGrabbers、Device.Runtime.FrameGrabbers.Euresys、Device.Runtime.FrameGrabbers.Matrox。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.FrameGrabbers;
using Device.Runtime.FrameGrabbers.Euresys;
using Device.Runtime.FrameGrabbers.Matrox;

namespace Device.Runtime.FrameGrabbers;

/// <summary>
/// 所有已实现采集卡品牌共用的工厂。
/// </summary>
/// <remarks>
/// 本类是内置采集卡驱动的唯一注册点；驱动专属校验与创建委托保存在同一注册项中。
/// </remarks>
public sealed class FrameGrabberFactory : IFrameGrabberFactory
{
    private readonly IReadOnlyDictionary<DeviceDriverId, Registration> _registrations;

    /// <summary>创建包含 Euresys MultiCam 和 Matrox Radient eV-CL 驱动的工厂。</summary>
    public FrameGrabberFactory()
    {
        var registrations = new[]
        {
            CreateRegistration(
                "frame-grabber.euresys.multicam",
                "Euresys",
                "MultiCam",
                "Euresys MultiCam 采集卡",
                options => new EuresysMultiCamFrameGrabber(options)),
            CreateRegistration(
                "frame-grabber.matrox.radient-evcl",
                "Matrox",
                "Radient eV-CL",
                "Matrox Radient eV-CL 采集卡",
                options => new MatroxRadientFrameGrabber(options),
                ValidateMatroxOptions),
        };

        AvailableDrivers = registrations
            .Select(x => x.Descriptor)
            .OrderBy(x => x.Brand, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Model, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _registrations = registrations.ToDictionary(x => x.Descriptor.Id);
    }

    /// <inheritdoc />
    public IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 Validate、TryGetValue、Create、Join、Select；具体条件和先后次序见方法体。
    /// </remarks>
    public IFrameGrabber Create(FrameGrabberOptions options)
    {
        FrameGrabberOptionsValidator.Validate(options);
        if (_registrations.TryGetValue(options.DriverId, out var registration))
        {
            registration.Validate?.Invoke(options);
            return registration.Create(options);
        }

        var installedDrivers = string.Join(", ", AvailableDrivers.Select(x => x.Id));
        throw new FrameGrabberDriverNotFoundException(options.DriverId, installedDrivers);
    }

    /// <summary>验证当前 Matrox 适配器能够安全发布的像素格式。</summary>
    private static void ValidateMatroxOptions(FrameGrabberOptions options)
    {
        if (!string.Equals(options.PixelFormat, "Mono8", StringComparison.OrdinalIgnoreCase))
        {
            throw new FrameGrabberConfigurationException(
                ["当前 Matrox Radient eV-CL 适配器只支持 Mono8 托管帧输出。"]);
        }
    }

    /// <summary>把描述、专属校验和延迟创建委托组装为唯一注册项。</summary>
    private static Registration CreateRegistration(
        string driverId,
        string brand,
        string model,
        string displayName,
        Func<FrameGrabberOptions, IFrameGrabber> create,
        Action<FrameGrabberOptions>? validate = null)
    {
        var descriptor = new DeviceDriverDescriptor(
            new DeviceDriverId(driverId),
            DeviceCategory.FrameGrabber,
            brand,
            model,
            displayName);
        return new Registration(descriptor, create, validate);
    }

    /// <summary>
    /// Registration 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
    /// </summary>
    private sealed record Registration(
        DeviceDriverDescriptor Descriptor,
        Func<FrameGrabberOptions, IFrameGrabber> Create,
        Action<FrameGrabberOptions>? Validate);
}
