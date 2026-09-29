using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Internal;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class MatConverterTests
{
    [Fact]
    public void FromVisionImage_IgnoresTrailingBufferBytes()
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 2, 3, 4, 99, 98]);

        using var mat = MatConverter.FromVisionImage(image);
        mat.GetArray(out byte[] pixels);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, pixels);
    }

    [Fact]
    public void FromVisionImage_RejectsBufferShortenedAfterConstruction()
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 2, 3, 4]);
        image.Pixels = [1, 2, 3];

        Assert.Throws<ArgumentException>(() => MatConverter.FromVisionImage(image));
    }
}
