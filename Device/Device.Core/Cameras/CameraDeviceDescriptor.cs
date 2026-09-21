using Device.Core.Devices;

namespace Device.Core.Cameras;

/// <summary>
/// 驱动枚举到的在线相机描述，供配置界面回填档案。
/// </summary>
/// <param name="DriverId">发现该设备所使用的驱动标识。</param>
/// <param name="Brand">品牌显示名。</param>
/// <param name="Model">型号显示名。</param>
/// <param name="SerialNumber">序列号，缺失时为空字符串。</param>
/// <param name="IpAddress">当前 IP，非 GigE 设备为空字符串。</param>
/// <param name="DisplayName">直接显示给用户的名称。</param>
public sealed record CameraDeviceDescriptor(
    DeviceDriverId DriverId,
    string Brand,
    string Model,
    string SerialNumber,
    string IpAddress,
    string DisplayName);
