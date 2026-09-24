using Device.Core.Cameras;
using Device.Core.Devices;
using FlowNodes.Device;

namespace FlowNodes.Workbench.Services;

/// <summary>
/// 工作台的 <see cref="ICameraProvider"/> 宿主实现。
/// </summary>
/// <remarks>
/// “怎样按名称找到相机”是宿主的组合职责，不是设备领域契约，因此这个类放在工作台而不是 Device.Core。
/// 它同时是缓存相机实例的所有者：相机在节点第一次按名称取用时才创建，由本类在释放时统一断开。
/// 生命周期写法与 Device.Runtime 的设备实现生命周期保持一致（互锁标记 + 幂等释放）。
/// </remarks>
public sealed class WorkbenchCameraProvider : ICameraProvider, IAsyncDisposable
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
    private readonly object _gate = new();
    private readonly Dictionary<string, ICamera> _cameras = new(StringComparer.OrdinalIgnoreCase);
    private int _disposeStarted;

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
        // 查配置与创建实例放在同一把锁内，保证并发按同一个名称取用时不会创建出两台相机。
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);

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

    /// <summary>断开并释放全部缓存的相机；重复调用保持幂等。</summary>
    /// <returns>释放完成的异步操作。</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        ICamera[] cameras;
        lock (_gate)
        {
            cameras = _cameras.Values.ToArray();
            _cameras.Clear();
        }

        foreach (var camera in cameras)
        {
            try
            {
                await camera.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // 单台相机释放失败不能中断其余相机的释放，也不应向退出流程再抛。
            }
        }
    }
}
