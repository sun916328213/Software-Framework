using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class CropToolTests
{
    [Fact]
    public async Task ExplicitClippedBounds_RespectStrideAndReportDirectSourceMapping()
    {
        byte[] pixels = [1, 2, 3, 99, 4, 5, 6, 99];
        var image = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 3, 2, PixelFormat.Mono8, pixels, 4);
        var request = ImageCropOperation.FromClippedBounds(image, new Rect2D(1.2, -0.5, 4, 3));
        Assert.Equal((1, 0, 2, 2), (request.X, request.Y, request.Width, request.Height));
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 2, 3, 5, 6 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(new Point2D(1, 0), result.Value.ImageToSource.Transform(new Point2D(0, 0)));
        Assert.Equal(image.FrameId, result.Value.FrameId);
        Assert.NotEqual(image.FrameId, result.Value.Image.FrameId);
        Assert.NotEqual(image.CoordinateSpaceId, result.Value.Image.CoordinateSpaceId);
        await engine.ReleaseContextAsync(context, default);
        Array.Clear(pixels);
        Assert.Equal(2, result.Value.Image.Pixels[0]);
    }

    [Theory]
    [InlineData(PixelFormat.Mono16, 2)]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public async Task IntegerCrop_CopiesWholePixelsInMultibytePaddedRows(PixelFormat format, int bpp)
    {
        var stride = 3 * bpp + 2;
        var pixels = Enumerable.Repeat((byte)99, 2 * stride).ToArray();
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                Array.Fill(pixels, (byte)(10 * y + x + 1), y * stride + x * bpp, bpp);
            }
        }
        var image = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 3, 2, format, pixels, stride);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new ImageCropOperation(image, 1, 0, 2, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 2, 3, 12, 13 }.SelectMany(value => Enumerable.Repeat(value, bpp)), result.Value!.Image.Pixels.ToArray());
        Assert.Equal(format, result.Value.Image.Format);
        Assert.Equal(image.FrameId, result.Value.FrameId);
    }

    [Fact]
    public void ExplicitClippingRejectsEmptyNonfiniteAndDisjointBounds_WhileStrictConstructorNeverClips()
    {
        var image = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 3, 2, PixelFormat.Mono8, new byte[6]);
        Assert.Throws<ArgumentNullException>(() => ImageCropOperation.FromClippedBounds(null!, new(0, 0, 1, 1)));
        Assert.Throws<ArgumentException>(() => ImageCropOperation.FromClippedBounds(image, new(99, 99, 10, 10)));
        Assert.Throws<ArgumentException>(() => ImageCropOperation.FromClippedBounds(image, new(-0.2, 0, 0.1, 1)));
        Assert.Throws<ArgumentException>(() => ImageCropOperation.FromClippedBounds(image, new(0, 0, 0, 1)));
        Assert.Throws<ArgumentException>(() => ImageCropOperation.FromClippedBounds(image, new(double.NaN, 0, 1, 1)));
        Assert.Throws<ArgumentException>(() => ImageCropOperation.FromClippedBounds(image, new(double.MaxValue, 0, double.MaxValue, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageCropOperation(image, 2, 0, 2, 1));
    }
}
