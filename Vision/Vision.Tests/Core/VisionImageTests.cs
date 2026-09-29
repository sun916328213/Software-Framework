using Vision.Core.Imaging;
using Xunit;

namespace Vision.Tests.Core;

[Trait("Category", "Unit")]
public sealed class VisionImageTests
{
    [Fact]
    public void FromBuffer_RejectsStrideShorterThanPixelRow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VisionImage.FromBuffer(3, 2, PixelFormat.Mono8, new byte[4], stride: 2));
    }
}
