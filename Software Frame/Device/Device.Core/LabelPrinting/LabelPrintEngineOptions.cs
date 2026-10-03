// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LabelPrintEngineOptions。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;

namespace Device.Core.LabelPrinting;

/// <summary>
/// 创建标签打印软件适配器所需的不可变配置。
/// </summary>
public sealed record LabelPrintEngineOptions
{
    /// <summary>打印引擎在应用中的唯一名称。</summary>
    public required string Name { get; init; }

    /// <summary>决定由哪个已安装驱动创建打印引擎。</summary>
    public required DeviceDriverId DriverId { get; init; }

    /// <summary>未在打印请求中指定时使用的打印机名称。</summary>
    public string? DefaultPrinterName { get; init; }

    /// <summary>等待一次打印任务完成的最长时间，单位为毫秒。</summary>
    public int PrintTimeoutMilliseconds { get; init; } = 10_000;

    /// <summary>导出预览图时使用的水平和垂直 DPI。</summary>
    public int PreviewDpi { get; init; } = 300;
}
