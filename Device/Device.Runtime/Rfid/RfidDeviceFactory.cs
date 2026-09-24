using Device.Core.Devices;
using Device.Core.Rfid;
using Device.Runtime.Rfid.Ck;
using Device.Runtime.Rfid.Xinuo;

namespace Device.Runtime.Rfid;

/// <summary>
/// 所有 RFID 品牌共用的工厂。
/// </summary>
/// <remarks>
/// 本类是内置 RFID 驱动的唯一注册点。新增品牌时只需在此增加一条注册。
/// </remarks>
public sealed class RfidDeviceFactory : IRfidDeviceFactory
{
    private readonly IReadOnlyDictionary<DeviceDriverId, Func<RfidOptions, IRfidDevice>> _creators;

    /// <summary>创建包含晨控 S650 和椿诺 XK15A-R01 驱动的工厂。</summary>
    public RfidDeviceFactory()
    {
        var registrations = new[]
        {
            CreateRegistration(
                "rfid.ck.s650.modbus-rtu",
                "CK",
                "S650",
                "晨控 S650 RFID",
                options => new CkS650RfidDevice(options)),
            CreateRegistration(
                "rfid.xinuo.xk15a-r01.serial",
                "Xinuo",
                "XK15A-R01",
                "椿诺 XK15A-R01 RFID",
                options => new XinuoXk15aR01RfidDevice(options)),
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
    public IRfidDevice Create(RfidOptions options)
    {
        RfidOptionsValidator.Validate(options);

        if (_creators.TryGetValue(options.DriverId, out var create))
            return create(options);

        var installedDrivers = string.Join(", ", AvailableDrivers.Select(x => x.Id));
        throw new RfidDriverNotFoundException(options.DriverId, installedDrivers);
    }

    /// <summary>把驱动描述和延迟创建委托组装为唯一注册项。</summary>
    private static Registration CreateRegistration(
        string driverId,
        string brand,
        string model,
        string displayName,
        Func<RfidOptions, IRfidDevice> create)
    {
        var descriptor = new DeviceDriverDescriptor(
            new DeviceDriverId(driverId),
            DeviceCategory.Rfid,
            brand,
            model,
            displayName);
        return new Registration(descriptor, create);
    }

    private sealed record Registration(
        DeviceDriverDescriptor Descriptor,
        Func<RfidOptions, IRfidDevice> Create);
}
