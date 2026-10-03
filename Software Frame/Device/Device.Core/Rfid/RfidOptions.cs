// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：RfidOptions。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;

namespace Device.Core.Rfid;

/// <summary>
/// 创建串口 RFID 读写器所需的不可变配置。
/// </summary>
public sealed record RfidOptions
{
    /// <summary>RFID 读写器在应用中的唯一名称。</summary>
    public required string Name { get; init; }

    /// <summary>决定由哪个已安装驱动创建读写器。</summary>
    public required DeviceDriverId DriverId { get; init; }

    /// <summary>读写器使用的串口名称，例如 <c>COM3</c>。</summary>
    public required string PortName { get; init; }

    /// <summary>串口波特率。</summary>
    public int BaudRate { get; init; } = 9_600;

    /// <summary>串口数据位。</summary>
    public int DataBits { get; init; } = 8;

    /// <summary>串口停止位，目前支持 1 或 2。</summary>
    public int StopBits { get; init; } = 1;

    /// <summary>串口校验方式：none、odd、even、mark 或 space。</summary>
    public string Parity { get; init; } = "none";

    /// <summary>Modbus RTU 设备的从站地址；串口文本协议驱动会忽略该值。</summary>
    public byte SlaveId { get; init; } = 1;

    /// <summary>每页可存储的字节数；当前驱动均为 8。</summary>
    public int PageSizeBytes { get; init; } = 8;

    /// <summary>单次读写超时时间，单位为毫秒。</summary>
    public int TimeoutMilliseconds { get; init; } = 3_000;
}
