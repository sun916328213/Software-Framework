// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CameraDeviceDescriptor。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
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
