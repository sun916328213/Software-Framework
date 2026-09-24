using Device.Core.Devices;
using Device.Core.Scanners;
using Device.Runtime.Scanners.Newland;

namespace Device.Runtime.Scanners;

/// <summary>
/// 所有扫码器品牌共用的工厂。
/// </summary>
/// <remarks>
/// 本类是内置扫码器驱动的唯一注册点。新增品牌时只需在此增加一条注册。
/// </remarks>
public sealed class ScannerFactory : IScannerFactory
{
    private readonly IReadOnlyDictionary<DeviceDriverId, Func<ScannerOptions, IScanner>> _creators;

    /// <summary>创建包含新大陆 NLS-NVH220 驱动的工厂。</summary>
    public ScannerFactory()
    {
        var registrations = new[]
        {
            CreateRegistration(
                "scanner.newland.nvh220.serial",
                "Newland",
                "NLS-NVH220",
                "新大陆 NLS-NVH220 扫码器",
                options => new NewlandNvh220Scanner(options)),
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
    public IScanner Create(ScannerOptions options)
    {
        ScannerOptionsValidator.Validate(options);

        if (_creators.TryGetValue(options.DriverId, out var create))
            return create(options);

        var installedDrivers = string.Join(", ", AvailableDrivers.Select(x => x.Id));
        throw new ScannerDriverNotFoundException(options.DriverId, installedDrivers);
    }

    /// <summary>把驱动描述和延迟创建委托组装为唯一注册项。</summary>
    private static Registration CreateRegistration(
        string driverId,
        string brand,
        string model,
        string displayName,
        Func<ScannerOptions, IScanner> create)
    {
        var descriptor = new DeviceDriverDescriptor(
            new DeviceDriverId(driverId),
            DeviceCategory.Scanner,
            brand,
            model,
            displayName);
        return new Registration(descriptor, create);
    }

    private sealed record Registration(
        DeviceDriverDescriptor Descriptor,
        Func<ScannerOptions, IScanner> Create);
}
