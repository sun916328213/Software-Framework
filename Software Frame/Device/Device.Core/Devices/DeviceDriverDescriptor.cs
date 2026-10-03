// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：DeviceDriverDescriptor。成员说明可结合下方具体实现阅读。
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
