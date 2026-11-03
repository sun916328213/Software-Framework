using Vision.Core.Imaging;
using Xunit;

namespace Vision.Tests.Core;

[Trait("Category", "Unit")]
public sealed class VisionImageSnapshotTests
{
    [Fact]
    public void Copy_CompactsPaddedRows_OwnsPixels_AndPreservesFrameIdentity()
    {
        var source = VisionImage.FromBuffer(3, 2, PixelFormat.Mono8, [1, 2, 3, 99, 4, 5, 6, 99], stride: 4, source: "test-frame");
        source.FrameId = 17;
        source.Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = VisionImageSnapshot.Copy(source);
        Assert.Equal(0, snapshot.Stride);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, snapshot.Pixels);
        Assert.Equal(source.FrameId, snapshot.FrameId);
        Assert.Equal(source.Timestamp, snapshot.Timestamp);
        Assert.Equal(source.Source, snapshot.Source);
        source.Pixels[0] = 55;
        Assert.Equal(1, snapshot.Pixels[0]);
    }

    [Fact]
    public void Copy_RejectsMalformedDeserializedBuffer()
    {
        var image = new VisionImage { Width = 3, Height = 2, Format = PixelFormat.Mono8, Pixels = [1, 2] };
        Assert.Throws<ArgumentException>(() => VisionImageSnapshot.Copy(image));
    }
}
