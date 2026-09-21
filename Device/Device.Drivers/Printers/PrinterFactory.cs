using Device.Core.Devices;
using Device.Core.Printers;
using Device.Drivers.Printers.Sato;
using Device.Drivers.Printers.Zebra;

namespace Device.Drivers.Printers;

/// <summary>
/// 所有已经实现的打印机品牌共用的工厂。
/// </summary>
/// <remarks>
/// 本类是内置打印机驱动的唯一注册点。新增品牌时只需在此增加一条注册，
/// 不需要在 Core 中同步维护驱动标识清单，也不增加品牌专用工厂类型。
/// </remarks>
public sealed class PrinterFactory : IPrinterFactory
{
    private readonly IReadOnlyDictionary<DeviceDriverId, Func<PrinterOptions, IPrinter>> _creators;

    /// <summary>创建包含 SATO SBPL 和 Zebra ZPL 网络驱动的打印机工厂。</summary>
    public PrinterFactory()
    {
        var registrations = new[]
        {
            CreateRegistration(
                "printer.sato.sbpl.tcp",
                "SATO",
                "SBPL / TCP",
                "SATO 网络标签打印机",
                options => new SatoNetworkPrinter(options)),
            CreateRegistration(
                "printer.zebra.zpl.tcp",
                "Zebra",
                "ZPL / TCP",
                "Zebra 网络标签打印机",
                options => new ZebraNetworkPrinter(options)),
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
    public IPrinter Create(PrinterOptions options)
    {
        PrinterOptionsValidator.Validate(options);

        if (_creators.TryGetValue(options.DriverId, out var create))
            return create(options);

        var installedDrivers = string.Join(", ", AvailableDrivers.Select(x => x.Id));
        throw new PrinterDriverNotFoundException(options.DriverId, installedDrivers);
    }

    /// <summary>把驱动描述和延迟创建委托组装为唯一注册项。</summary>
    private static Registration CreateRegistration(
        string driverId,
        string brand,
        string model,
        string displayName,
        Func<PrinterOptions, IPrinter> create)
    {
        var descriptor = new DeviceDriverDescriptor(
            new DeviceDriverId(driverId),
            DeviceCategory.Printer,
            brand,
            model,
            displayName);
        return new Registration(descriptor, create);
    }

    private sealed record Registration(
        DeviceDriverDescriptor Descriptor,
        Func<PrinterOptions, IPrinter> Create);
}
