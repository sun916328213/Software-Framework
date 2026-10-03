// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：PrinterFaultCodes。成员说明可结合下方具体实现阅读。
namespace Device.Core.Printers;

/// <summary>
/// <see cref="PrinterStatus.AdditionalFaultCode"/> 可返回的强命名故障码。
/// </summary>
/// <remarks>
/// 使用字符串而不是枚举，后续驱动可以增加新故障而不修改公共类型；
/// 业务代码应引用这些常量，避免手写字符串产生拼写错误。
/// </remarks>
public static class PrinterFaultCodes
{
    /// <summary>标签、碳带路径或其他介质状态异常。</summary>
    public const string MediaError = "media_error";

    /// <summary>标签检测等传感器异常。</summary>
    public const string SensorError = "sensor_error";

    /// <summary>打印头硬件异常。</summary>
    public const string HeadError = "head_error";

    /// <summary>打印机外盖处于打开状态。</summary>
    public const string CoverOpen = "cover_open";

    /// <summary>存储卡或其他存储介质异常。</summary>
    public const string StorageError = "storage_error";

    /// <summary>切刀硬件异常。</summary>
    public const string CutterError = "cutter_error";

    /// <summary>厂商未进一步分类的打印机错误。</summary>
    public const string OtherError = "other_error";

    /// <summary>切刀位置传感器异常。</summary>
    public const string CutterSensorError = "cutter_sensor_error";

    /// <summary>堆叠器或回卷器已满。</summary>
    public const string StackerFull = "stacker_full";

    /// <summary>RFID 标签读写异常。</summary>
    public const string RfidTagError = "rfid_tag_error";

    /// <summary>RFID 标签受到写保护。</summary>
    public const string RfidProtectError = "rfid_protect_error";

    /// <summary>打印机电池异常。</summary>
    public const string BatteryError = "battery_error";

    /// <summary>驱动确认设备返回错误状态，但当前版本尚未识别具体含义。</summary>
    public const string UnknownError = "unknown_error";
}
