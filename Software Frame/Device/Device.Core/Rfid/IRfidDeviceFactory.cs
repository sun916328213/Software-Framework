// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：IRfidDeviceFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.Rfid。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.Rfid;

namespace Device.Core.Rfid;

/// <summary>
/// 所有 RFID 品牌共用的创建接口。
/// </summary>
/// <remarks>
/// 调用方从 <see cref="AvailableDrivers"/> 选择驱动，并保存描述中的稳定标识，
/// 不需要引用品牌枚举、品牌专用工厂或手写驱动标识。
/// </remarks>
public interface IRfidDeviceFactory
{
    /// <summary>当前工厂已经实现、可供配置界面选择的 RFID 驱动。</summary>
    IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    /// <summary>根据配置中的驱动标识创建 RFID 设备。</summary>
    /// <param name="options">RFID 配置。</param>
    /// <returns>尚未连接的 RFID 实例。</returns>
    /// <exception cref="RfidConfigurationException">配置不符合 RFID 约束。</exception>
    /// <exception cref="RfidDriverNotFoundException">配置的驱动未由该工厂实现。</exception>
    IRfidDevice Create(RfidOptions options);
}
