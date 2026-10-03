// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LabelPreview。成员说明可结合下方具体实现阅读。
namespace Device.Core.LabelPrinting;

/// <summary>
/// 已经脱离临时文件生命周期的标签预览图。
/// </summary>
/// <param name="MediaType">图像媒体类型，例如 image/png。</param>
/// <param name="Data">完整图像文件字节。</param>
public sealed record LabelPreview(string MediaType, ReadOnlyMemory<byte> Data);
