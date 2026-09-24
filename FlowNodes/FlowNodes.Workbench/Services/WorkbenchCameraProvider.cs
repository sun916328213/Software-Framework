using Device.Core.Cameras;
using Device.Core.Devices;
using FlowNodes.Device;

namespace FlowNodes.Workbench.Services;

/// <summary>
/// 工作台的 <see cref="ICameraProvider"/> 宿主实现。
/// </summary>
/// <remarks>
/// “怎样按名称找到相机”是宿主的组合职责，不是设备领域契约，因此这个类放在工作台而不是 Device.Core。
/// 相机实例在节点第一次按名称取用时才创建，构造过程和启动路径都不枚举硬件、不连接硬件。
/// </remarks>
public sealed class WorkbenchCameraProvider : ICameraProvider
{
    /// <summary>
    /// 工作台演示配置；真实宿主应改为从自己的配置源读取相机配置。
    /// </summary>
    private static readonly CameraOptions[] DemoConfigurations =
    [
        new CameraOptions
        {
            Name = "Camera01",
            DriverId = new DeviceDriverId("camera.hik.gige"),
            IpAddress = "192.168.1.10",
        },
    ];

    private readonly ICameraFactory _factory;
    private readonly Dictionary<string, ICamera> _cameras = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>创建提供器；此时不创建任何相机实例。</summary>
    /// <param name="factory">按驱动标识创建相机的工厂。</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为 <see langword="null"/>。</exception>
    public WorkbenchCameraProvider(ICameraFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Names =>
        DemoConfigurations
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <inheritdoc />
    public ICamera Get(string name)
    {
        if (_cameras.TryGetValue(name, out var camera))
            return camera;

        var options = Array.Find(
            DemoConfigurations,
            x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        if (options is null)
            throw new KeyNotFoundException($"没有名称为 '{name}' 的相机配置。");

        camera = _factory.Create(options);
        _cameras[name] = camera;
        return camera;
    }
}
