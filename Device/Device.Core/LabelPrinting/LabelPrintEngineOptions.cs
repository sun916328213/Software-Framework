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
