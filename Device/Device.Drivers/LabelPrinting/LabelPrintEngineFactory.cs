using Device.Core.Devices;
using Device.Core.LabelPrinting;
using Device.Drivers.LabelPrinting.BarTender;

namespace Device.Drivers.LabelPrinting;

/// <summary>
/// 所有已实现标签打印软件共用的工厂。
/// </summary>
/// <remarks>
/// 本类是内置标签打印引擎驱动的唯一注册点。新增实现时只需在此增加一条注册。
/// </remarks>
public sealed class LabelPrintEngineFactory : ILabelPrintEngineFactory
{
    private readonly IReadOnlyDictionary<DeviceDriverId, Func<LabelPrintEngineOptions, ILabelPrintEngine>> _creators;

    /// <summary>创建包含 BarTender SDK 驱动的工厂。</summary>
    public LabelPrintEngineFactory()
    {
        var registrations = new[]
        {
            CreateRegistration(
                "label-print.bartender.sdk",
                "Seagull Scientific",
                "BarTender SDK",
                "BarTender 标签打印引擎",
                options => new BarTenderLabelPrintEngine(options)),
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
    public ILabelPrintEngine Create(LabelPrintEngineOptions options)
    {
        LabelPrintEngineOptionsValidator.Validate(options);
        if (_creators.TryGetValue(options.DriverId, out var create))
            return create(options);

        var installedDrivers = string.Join(", ", AvailableDrivers.Select(x => x.Id));
        throw new LabelPrintEngineDriverNotFoundException(options.DriverId, installedDrivers);
    }

    /// <summary>把驱动描述和延迟创建委托组装为唯一注册项。</summary>
    private static Registration CreateRegistration(
        string driverId,
        string brand,
        string model,
        string displayName,
        Func<LabelPrintEngineOptions, ILabelPrintEngine> create)
    {
        var descriptor = new DeviceDriverDescriptor(
            new DeviceDriverId(driverId),
            DeviceCategory.LabelPrintEngine,
            brand,
            model,
            displayName);
        return new Registration(descriptor, create);
    }

    private sealed record Registration(
        DeviceDriverDescriptor Descriptor,
        Func<LabelPrintEngineOptions, ILabelPrintEngine> Create);
}
