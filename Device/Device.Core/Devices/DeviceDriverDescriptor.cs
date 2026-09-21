namespace Device.Core.Devices;

/// <summary>
/// 驱动展示信息，用于配置界面生成品牌和型号选择项。
/// </summary>
/// <param name="Id">配置文件中保存的稳定驱动标识。</param>
/// <param name="Category">驱动所属设备类别。</param>
/// <param name="Brand">供筛选和展示使用的品牌名称。</param>
/// <param name="Model">驱动支持的型号或协议族。</param>
/// <param name="DisplayName">直接显示给用户的名称。</param>
public sealed record DeviceDriverDescriptor(
    DeviceDriverId Id,
    DeviceCategory Category,
    string Brand,
    string Model,
    string DisplayName);
