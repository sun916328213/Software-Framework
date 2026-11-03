#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 不缩放、不旋转的整数像素矩形裁剪。左上包含、右下不包含，越界明确拒绝而非静默裁切。
/// </summary>
public sealed class ImageCropOperation : VisionImageOperation<ImageCropOutput>
{
    public const string Id = "vision.processing.crop";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    /// <summary>源图中的起始列，单位为像素。</summary>
    public int X
    {
        get;
    }

    /// <summary>源图中的起始行，单位为像素。</summary>
    public int Y
    {
        get;
    }

    /// <summary>裁剪宽度，单位为像素。</summary>
    public int Width
    {
        get;
    }

    /// <summary>裁剪高度，单位为像素。</summary>
    public int Height
    {
        get;
    }

    public ImageCropOperation(VisionFrame image, int x, int y, int width, int height) : base(image)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0
            || (long)x + width > image.Width || (long)y + height > image.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "裁剪矩形必须非空并完整位于输入图像内。");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// 显式将有限浮点半开矩形与输入视野相交，再向外取整为整数像素矩形。
    /// 无几何交集、零面积或端点溢出时拒绝；不会改变普通构造器的严格越界规则。
    /// </summary>
    public static ImageCropOperation FromClippedBounds(VisionFrame image, Rect2D bounds)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y)
            || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)
            || bounds.Width <= 0 || bounds.Height <= 0
            || !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom))
        {
            throw new ArgumentException("裁剪边界和正尺寸必须有限且端点可表示。", nameof(bounds));
        }

        var left = Math.Max(0, bounds.Left);
        var top = Math.Max(0, bounds.Top);
        var right = Math.Min(image.Width, bounds.Right);
        var bottom = Math.Min(image.Height, bounds.Bottom);
        if (left >= right || top >= bottom)
        {
            throw new ArgumentException("裁剪边界与输入图像没有有效面积交集。", nameof(bounds));
        }

        var x = (int)Math.Floor(left);
        var y = (int)Math.Floor(top);
        var endX = (int)Math.Ceiling(right);
        var endY = (int)Math.Ceiling(bottom);
        return new ImageCropOperation(image, x, y, endX - x, endY - y);
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
