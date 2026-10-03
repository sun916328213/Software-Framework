// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LabelPrintRequest、ILabelJobService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;

namespace Device.Core.LabelPrinting;

/// <summary>
/// LabelPrintRequest 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
/// </summary>
public sealed record LabelPrintRequest(string TemplatePath, string? PrinterName = null,
    IReadOnlyDictionary<string, string>? Fields = null, int Copies = 1);

/// <summary>Runs a complete template job without exposing the mutable engine to the caller.</summary>
/// <remarks>
/// ILabelJobService 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
/// </remarks>
public interface ILabelJobService
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PrintAsync。
    /// </summary>
    Task PrintAsync(LabelPrintEngineOptions options, LabelPrintRequest request, CancellationToken cancellationToken = default);
}
