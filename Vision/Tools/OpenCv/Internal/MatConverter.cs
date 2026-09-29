// ─────────────────────────────────────────────────────────────────────────────
// P4 —— VisionImage ↔ Mat 边界转换（计划 §7「Input 使用 VisionImage 等公共语义」）。
//
// 方向：Vision.Core 只认 VisionImage；OpenCV 只认 Mat。转换器放在
// Tools.OpenCv/Internal，Mat 不得出现在 Parameters / Result 的公开签名里。
// P4 只实现 Mono8 紧凑排布（首批 Tool 的主力格式）；其他格式抛明确异常。
// ─────────────────────────────────────────────────────────────────────────────

using OpenCvSharp;
using Vision.Core.Imaging;

namespace Vision.Tools.OpenCv.Internal;

/// <summary>
/// 图像边界转换（VisionImage ↔ Mat）。
/// </summary>
public static class MatConverter
{
    /// <summary>
    /// 由 <see cref="VisionImage"/> 生成 Mat（Mono8，拷贝像素）。
    /// 调用方负责释放返回的 <see cref="Mat"/>。
    /// </summary>
    public static Mat FromVisionImage(VisionImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.IsEmpty)
            throw new ArgumentException("图像为空（宽高/像素缺失）。", nameof(image));
        if (image.Format != PixelFormat.Mono8)
            throw new NotSupportedException($"P4 仅支持 Mono8，实际 {image.Format}。");
        if (image.Pixels == null || image.Pixels.Length == 0)
            throw new ArgumentException("像素缓冲为空。", nameof(image));
        if (image.Stride != 0 && image.Stride != image.ExpectedStride)
            throw new NotSupportedException($"P4 仅支持紧凑排布，实际 stride={image.Stride}（期望 {image.ExpectedStride}）。");

        var mat = new Mat(image.Height, image.Width, MatType.CV_8UC1);
        System.Runtime.InteropServices.Marshal.Copy(image.Pixels, 0, mat.Data, image.Pixels.Length);
        return mat;
    }

    /// <summary>
    /// 由单通道 8 位 Mat 生成 <see cref="VisionImage"/>（拷贝像素；供处理类 Tool 链式传递）。
    /// </summary>
    public static VisionImage ToVisionImage(Mat mat, string source = "")
    {
        ArgumentNullException.ThrowIfNull(mat);
        if (mat.Empty())
            throw new ArgumentException("Mat 为空。", nameof(mat));
        if (mat.Type() != MatType.CV_8UC1 || !mat.IsContinuous())
            throw new NotSupportedException($"P4 仅支持连续 CV_8UC1，实际 {mat.Type()} continuous={mat.IsContinuous()}。");

        mat.GetArray(out byte[] pixels);
        return VisionImage.FromBuffer(mat.Width, mat.Height, PixelFormat.Mono8, pixels, source: source);
    }
}
