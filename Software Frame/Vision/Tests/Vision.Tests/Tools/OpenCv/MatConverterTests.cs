using System.Runtime.InteropServices;
using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Imaging;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

/// <summary>旧转换器测试迁至实际复制边界；使用真实OpenCV Mat，不新增公开SDK入口或可变图像。</summary>
[Trait("Category", "Unit")]
public sealed class MatConverterTests
{
    [Fact]
    public void CopyFrame_IgnoresTrailingBufferBytes()
    {
        var frame = Frame(2, 2, PixelFormat.Mono8, [1, 2, 3, 4, 99, 98]);
        using var mat = OpenCvFrameAdapter.CopyGray(frame);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Bytes(mat));
        Assert.Equal(4, frame.Pixels.Length);
    }

    [Fact]
    public void ShortBufferIsRejectedBeforeNativeCopy_AndFrameCannotBeShortenedAfterConstruction()
    {
        Assert.Throws<ArgumentException>(() => Frame(2, 2, PixelFormat.Mono8, [1, 2, 3]));
        var pixels = new byte[] { 1, 2, 3, 4 };
        var frame = Frame(2, 2, PixelFormat.Mono8, pixels);
        Array.Clear(pixels);
        using var mat = OpenCvFrameAdapter.CopyGray(frame);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Bytes(mat));
        Assert.False(typeof(VisionFrame).GetProperty(nameof(VisionFrame.Pixels))!.CanWrite);
    }

    [Theory]
    [InlineData(PixelFormat.Mono8, 1)]
    [InlineData(PixelFormat.Mono16, 2)]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public void CopyPreservesRawOrderAndNativeType_IgnoresPadding_AndOwnsNativeMemory(PixelFormat format, int bpp)
    {
        var expected = Enumerable.Range(1, 4 * bpp).Select(value => (byte)value).ToArray();
        var stride = 2 * bpp + 2;
        var padded = Enumerable.Repeat((byte)99, 2 * stride).ToArray();
        expected.AsSpan(0, 2 * bpp).CopyTo(padded);
        expected.AsSpan(2 * bpp, 2 * bpp).CopyTo(padded.AsSpan(stride));
        var frame = Frame(2, 2, format, padded, stride);
        var original = frame.Pixels.ToArray();
        Array.Clear(padded);
        using var mat = OpenCvFrameAdapter.Copy(frame);
        Assert.Equal(TypeFor(format), mat.Type());
        Assert.Equal(expected, Bytes(mat));
        Assert.Equal((2, 2), (mat.Width, mat.Height));
        // 修改原生输出也不得回写Core输入；RGB/BGR仅复制存储，不在这里偷偷交换通道。
        mat.SetTo(Scalar.All(0));
        Assert.Equal(original, frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public void GrayCopyRejectsOtherFormats_WithoutImplicitConversion(PixelFormat format, int bpp)
    {
        var frame = Frame(1, 1, format, new byte[bpp]);
        Assert.Throws<NotSupportedException>(() => OpenCvFrameAdapter.CopyGray(frame));
    }

    [Theory]
    [InlineData(PixelFormat.Mono8, 1)]
    [InlineData(PixelFormat.Mono16, 2)]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public void SnapshotCopiesNonContinuousRows_AndOutlivesMutatedAndReleasedNativeImages(PixelFormat format, int bpp)
    {
        var pixels = Enumerable.Range(1, 5 * 4 * bpp).Select(value => (byte)value).ToArray();
        var expected = pixels.AsSpan(6 * bpp, 2 * bpp).ToArray()
            .Concat(pixels.AsSpan(11 * bpp, 2 * bpp).ToArray()).ToArray();
        var space = Guid.NewGuid();
        VisionFrame snapshot;
        using (var parent = new Mat(4, 5, TypeFor(format)))
        {
            Marshal.Copy(pixels, 0, parent.Data, pixels.Length);
            using var view = new Mat(parent, new Rect(1, 1, 2, 2));
            Assert.False(view.IsContinuous());
            snapshot = OpenCvFrameAdapter.Snapshot(view, format, space);
            view.SetTo(Scalar.All(0));
        }

        Assert.Equal(expected, snapshot.Pixels.ToArray());
        Assert.Equal(space, snapshot.CoordinateSpaceId);
        Assert.NotEqual(Guid.Empty, snapshot.FrameId);
        Assert.Equal((2, 2, 2 * bpp, format), (snapshot.Width, snapshot.Height, snapshot.Stride, snapshot.Format));
    }

    [Fact]
    public void SnapshotRejectsEmptyOrMismatchedStorage_WithoutTakingCallerOwnership()
    {
        using var empty = new Mat();
        Assert.Throws<InvalidDataException>(() => OpenCvFrameAdapter.Snapshot(empty, PixelFormat.Mono8, Guid.NewGuid()));
        using var source = new Mat(2, 2, MatType.CV_16UC1, Scalar.All(7));
        Assert.Throws<InvalidDataException>(() => OpenCvFrameAdapter.Snapshot(source, PixelFormat.Mono8, Guid.NewGuid()));
        // 异常不代替调用者释放其Mat，后续仍能使用同一原生对象。
        source.SetTo(Scalar.All(8));
        Assert.Equal((ushort)8, source.At<ushort>(0, 0));
    }

    [Theory]
    [InlineData(PixelFormat.Mono8)]
    [InlineData(PixelFormat.Mono16)]
    [InlineData(PixelFormat.Bgr24)]
    [InlineData(PixelFormat.Bgra32)]
    [InlineData(PixelFormat.Float32)]
    public void DecodedFormatPreservesSupportedStorage_AndDeclaresBgrNotRgb(PixelFormat format)
    {
        using var source = new Mat(1, 1, TypeFor(format));
        Assert.Equal(format, OpenCvFrameAdapter.DecodedFormat(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedDecodedStorageIsRejected_WithoutReducingBitDepthOrDroppingChannels(bool color)
    {
        using var source = new Mat(1, 1, color ? MatType.CV_16UC3 : MatType.CV_64FC1);
        Assert.Throws<NotSupportedException>(() => OpenCvFrameAdapter.DecodedFormat(source));
    }

    private static VisionFrame Frame(int width, int height, PixelFormat format, byte[] bytes, int stride = 0) =>
        new(Guid.NewGuid(), Guid.NewGuid(), width, height, format, bytes, stride);

    // 仅用于本测试创建已知原生存储；期望字节由输入布局独立给出，不调用生产映射函数反算。
    private static MatType TypeFor(PixelFormat format) => format switch
    {
        PixelFormat.Mono8 => MatType.CV_8UC1,
        PixelFormat.Mono16 => MatType.CV_16UC1,
        PixelFormat.Rgb24 or PixelFormat.Bgr24 => MatType.CV_8UC3,
        PixelFormat.Bgra32 => MatType.CV_8UC4,
        PixelFormat.Float32 => MatType.CV_32FC1,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static byte[] Bytes(Mat continuous)
    {
        var bytes = new byte[checked((int)(continuous.Total() * continuous.ElemSize()))];
        Marshal.Copy(continuous.Data, bytes, 0, bytes.Length);
        return bytes;
    }
}
