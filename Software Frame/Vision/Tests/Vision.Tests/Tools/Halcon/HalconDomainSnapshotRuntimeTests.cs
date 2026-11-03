using HalconDotNet;
using Vision.Core.Imaging;
using Vision.Tools.Halcon.Internal;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

/// <summary>必须在授权HALCON环境验证；托管UI/编译不抵扣域与原生生命周期。</summary>
[Trait("Category", "RequiresRuntime")]
public sealed class HalconDomainSnapshotRuntimeTests
{
    [Theory(Skip = "需要HALCON Runtime/License；验证真实domain与指针矩形存储，禁止用fake/其他后端抵扣。")]
    [InlineData(PixelFormat.Mono8, "reduced")]
    [InlineData(PixelFormat.Mono16, "reduced")]
    [InlineData(PixelFormat.Rgb24, "reduced")]
    [InlineData(PixelFormat.Mono8, "hole")]
    [InlineData(PixelFormat.Mono8, "empty")]
    [InlineData(PixelFormat.Mono8, "shifted")]
    public void NonFullDomain_IsRejectedBeforePublicPixelCopy_AndBorrowedImageSurvives(PixelFormat format, string kind)
    {
        var bpp = format == PixelFormat.Mono8 ? 1 : format == PixelFormat.Mono16 ? 2 : 3;
        var pixels = Enumerable.Repeat((byte)73, 12 * bpp).ToArray();
        using var native = HalconImageConverter.FromDisplaySnapshot(VisionImage.FromBuffer(4, 3, format, pixels));
        HObject region;
        if (kind == "empty") HOperatorSet.GenEmptyRegion(out region);
        else if (kind == "hole")
        {
            HOperatorSet.GenRectangle1(out var full, 0, 0, 2, 3);
            using (full)
            {
                HOperatorSet.GenRectangle1(out var hole, 1, 1, 1, 1);
                using (hole) HOperatorSet.Difference(full, hole, out region);
            }
        }
        else HOperatorSet.GenRectangle1(out region, 0, kind == "shifted" ? 1 : 0, kind == "shifted" ? 2 : 1, kind == "shifted" ? 4 : 1);
        using (region)
        {
            if (kind == "shifted")
            {
                HOperatorSet.AreaCenter(region, out var area, out var row, out var column);
                using (area) using (row) using (column) Assert.Equal(12, area.D); // 面积相等也不能代表完整像素域。
            }
            HOperatorSet.ChangeDomain(native, region, out var restricted);
            using (restricted)
            {
                var error = Assert.Throws<NotSupportedException>(() => HalconImageConverter.ToVisionImage(restricted));
                Assert.Contains("域", error.Message); Assert.Equal((4, 3), HalconImageConverter.GetSize(restricted));
            }
        }
        var actual = HalconImageConverter.ToVisionImage(native);
        Assert.Equal(pixels, actual.Pixels); // 拒绝不释放/改写调用方原图，完整图仍可读取。
    }
}
