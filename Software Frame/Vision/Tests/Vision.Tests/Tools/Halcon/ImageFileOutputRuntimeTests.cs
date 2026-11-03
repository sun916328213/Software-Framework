using HalconDotNet;
using Vision.Core.Imaging;
using Vision.Tools.Halcon.Acquisition;
using Vision.Tools.Halcon.Internal;
using Vision.Tools.OpenCv.Processing;
using Xunit;
using CvBlob = Vision.Tools.OpenCv.Inspection.BlobTool;

namespace Vision.Tests.Tools.Halcon;

[Trait("Category", "RequiresRuntime")]
public sealed class ImageFileOutputRuntimeTests
{
    [Theory(Skip = "需要HALCON Runtime和License；仅在受控授权离线环境执行，不能用OpenCV同名源抵扣。")]
    [InlineData(PixelFormat.Mono8)]
    [InlineData(PixelFormat.Mono16)]
    [InlineData(PixelFormat.Rgb24)]
    public void NativeReadImage_StandaloneAndRealConvertThresholdBlob_KeepOwnedSnapshot(PixelFormat format)
    {
        var path = Path.Combine(Path.GetTempPath(), "halcon-source-" + Guid.NewGuid() + ".tiff");
        try
        {
            var bpp = format == PixelFormat.Mono8 ? 1 : format == PixelFormat.Mono16 ? 2 : 3;
            var pixels = new byte[81 * bpp];
            for (var y = 0; y < 9; y++) for (var x = 0; x < 9; x++)
            {
                var foreground = x >= 2 && x <= 5 && y >= 3 && y <= 6; var index = (y * 9 + x) * bpp;
                if (format == PixelFormat.Mono16)
                { var v = foreground ? 20000 : 10000; pixels[index] = (byte)(v & 255); pixels[index + 1] = (byte)(v >> 8); }
                else for (var c = 0; c < bpp; c++) pixels[index + c] = foreground ? (byte)180 : (byte)0;
            }
            using (var native = HalconImageConverter.FromDisplaySnapshot(VisionImage.FromBuffer(9, 9, format, pixels)))
                HOperatorSet.WriteImage(native, "tiff", 0, path);
            using var source = new ImageFileTool { Parameters = new() { FilePath = path } };
            using var read = source.RunImage(null);
            Assert.True(read.Success, read.Message); Assert.NotNull(source.LoadedImage);
            var snapshot = Assert.IsType<VisionImage>(read.OutputImage);
            Assert.Equal(format, snapshot.Format); Assert.Equal(pixels, snapshot.Pixels);
            Assert.Equal((9, 9), (read.Width, read.Height));
            using var convert = new ImageConvertTool { Parameters = new() { Mono16Minimum = 10000, Mono16Maximum = 20000 } };
            using var gray = convert.Run(snapshot); Assert.True(gray.Success, gray.Message);
            using var threshold = new ThresholdTool { Parameters = new() { MinGray = 100 } };
            using var mask = threshold.Run(gray.OutputImage); Assert.True(mask.Success, mask.Message); Assert.Equal(16, mask.WhitePixelCount);
            using var blob = new CvBlob(); blob.Parameters.MinGray = 128; blob.Parameters.MinArea = 1;
            using var measured = blob.Run(mask.OutputImage);
            Assert.True(measured.Success, measured.Message);
            var item = Assert.Single(measured.Items); Assert.Equal(16, item.Area); Assert.Equal(new Vision.Core.Geometry.Rect2D(2, 3, 4, 4), item.Bounds);
            source.Parameters.FilePath = path + ".missing";
            using var missing = source.RunImage(null); Assert.False(missing.Success); Assert.Null(missing.OutputImage); Assert.Null(source.LoadedImage);
            source.Dispose(); Assert.Equal(pixels, snapshot.Pixels);
            read.Dispose(); Assert.Null(read.OutputImage); Assert.Equal(pixels, snapshot.Pixels);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact(Skip = "需要HALCON Runtime和License；实际TIFF域掩膜保存/读取需授权环境验证。")]
    public void ReducedDomain_IsNotSilentlyDroppedFromPublicSnapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), "halcon-domain-" + Guid.NewGuid() + ".tiff");
        try
        {
            using var native = HalconImageConverter.FromDisplaySnapshot(VisionImage.FromBuffer(3, 2, PixelFormat.Mono8, [1, 2, 3, 4, 5, 6]));
            HOperatorSet.GenRectangle1(out var region, 0, 0, 1, 0);
            using (region)
            {
                HOperatorSet.ReduceDomain(native, region, out var reduced);
                using (reduced) HOperatorSet.WriteImage(reduced, "tiff", 0, path);
            }
            using var source = new ImageFileTool { Parameters = new() { FilePath = path } };
            using var result = source.RunImage(null);
            Assert.True(result.Success, result.Message); Assert.NotNull(source.LoadedImage);
            Assert.Null(result.OutputImage); Assert.Contains("域", result.Message);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact(Skip = "需要HALCON Runtime和License；保留旧原生读图消费者的非公共快照格式兼容。")]
    public void NativeFloatImage_PreservesLegacyDimensionsAndBorrowedImage_ButExplicitlyHasNoPublicOutput()
    {
        var path = Path.Combine(Path.GetTempPath(), "halcon-real-" + Guid.NewGuid() + ".tiff");
        try
        {
            HOperatorSet.GenImageConst(out var native, "real", 3, 2);
            using (native) HOperatorSet.WriteImage(native, "tiff", 0, path);
            using var source = new ImageFileTool { Parameters = new() { FilePath = path } };
            using var result = source.RunImage(null);
            Assert.True(result.Success, result.Message); Assert.Equal((3, 2), (result.Width, result.Height));
            Assert.NotNull(source.LoadedImage); Assert.Null(result.OutputImage);
            Assert.Contains("公共图像输出不可用", result.Message);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
