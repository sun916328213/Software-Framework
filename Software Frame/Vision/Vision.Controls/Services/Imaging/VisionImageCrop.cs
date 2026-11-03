using Vision.Core.Imaging;
using Vision.Core.Roi;
using Vision.Tools.OpenCv.Processing;

namespace Vision.Controls.Services;

/// <summary>模板取样及旧裁剪 API 的 ROI 适配；像素裁剪逻辑统一在 Tools。</summary>
public static class VisionImageCrop
{
    public static VisionImage Crop(VisionImage image, Roi roi)
    {
        if (roi.Kind != RoiKind.Rectangle || Math.Abs(roi.Pose.AngleDeg) > 1e-9)
            throw new InvalidOperationException("请选中轴对齐矩形；圆和旋转区域不能作为矩形裁剪输入。");
        try { return ImageRegionCrop.Crop(image, roi.Bounds).OutputImage; }
        catch (ArgumentException error) { throw new InvalidOperationException(error.Message, error); }
    }
}
