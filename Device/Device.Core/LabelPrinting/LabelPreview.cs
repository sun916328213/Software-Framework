namespace Device.Core.LabelPrinting;

/// <summary>
/// 已经脱离临时文件生命周期的标签预览图。
/// </summary>
/// <param name="MediaType">图像媒体类型，例如 image/png。</param>
/// <param name="Data">完整图像文件字节。</param>
public sealed record LabelPreview(string MediaType, ReadOnlyMemory<byte> Data);
