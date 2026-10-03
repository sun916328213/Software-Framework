// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：HalconImageConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Runtime.InteropServices、HalconDotNet、Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P3 —— VisionImage ↔ HObject 边界转换（计划 §7「Input 使用 VisionImage / ROI / Pose 等公共语义」）。
//
// 方向：Vision.Core 只认 VisionImage（P2 已立）；HALCON 只认 HObject。
// 转换器放在 Tools.Halcon/Internal，Core 保持干净（CorePurityTests 守卫）。
// P3 只实现 Mono8（Blob/模板/条码的主力格式）；其他格式抛明确异常，P4 按需扩充。
// ─────────────────────────────────────────────────────────────────────────────

using System.Runtime.InteropServices;
using HalconDotNet;
using Vision.Core.Imaging;

namespace Vision.Tools.Halcon.Internal;

/// <summary>
/// 图像边界转换（VisionImage ↔ HObject）。
/// </summary>
public static class HalconImageConverter
{
    /// <summary>
    /// 由 <see cref="VisionImage"/> 生成 HALCON 图像（Mono8）。
    /// 调用方负责释放返回的 <see cref="HObject"/>。
    /// </summary>
    public static HObject FromVisionImage(VisionImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.IsEmpty)
            throw new ArgumentException("图像为空（宽高/像素缺失）。", nameof(image));
        if (image.Format != PixelFormat.Mono8)
            throw new NotSupportedException($"P3 仅支持 Mono8，实际 {image.Format}（其他格式 P4 按需扩充）。");
        if (image.Pixels == null || image.Pixels.Length == 0)
            throw new ArgumentException("像素缓冲为空。", nameof(image));
        if (image.Stride != 0 && image.Stride != image.ExpectedStride)
            throw new NotSupportedException($"P3 仅支持紧凑排布，实际 stride={image.Stride}（期望 {image.ExpectedStride}）。");
        var pixelCount = checked(image.Width * image.Height);
        if (image.Pixels.Length < pixelCount)
            throw new ArgumentException($"像素缓冲长度 {image.Pixels.Length} 小于图像所需的 {pixelCount} 字节。", nameof(image));

        var handle = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
        try
        {
            HOperatorSet.GenImage1(out var hImage, "byte", image.Width, image.Height, handle.AddrOfPinnedObject());
            return hImage;
        }
        finally
        {
            // GenImage1 已拷贝像素（HALCON 文档：指针内容被复制），此处即可解 pin。
            handle.Free();
        }
    }

    /// <summary>读取 HALCON 图像尺寸（不拷贝像素）。</summary>
    public static (int Width, int Height) GetSize(HObject image)
    {
        ArgumentNullException.ThrowIfNull(image);
        HOperatorSet.GetImageSize(image, out var w, out var h);
        try
        {
            return (w.I, h.I);
        }
        finally
        {
            w.Dispose();
            h.Dispose();
        }
    }
}
