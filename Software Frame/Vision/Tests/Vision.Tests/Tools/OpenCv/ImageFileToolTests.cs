using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;
using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Tests.Tools.OpenCv;

/// <summary>文件输入旧需求迁至无图请求；真实离线编码/解码，不借用旧Tool或可变结果。</summary>
[Trait("Category", "Unit")]
public sealed class ImageFileToolTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vision-image-file-" + Guid.NewGuid());
    public ImageFileToolTests() => Directory.CreateDirectory(_folder);
    public void Dispose() => Directory.Delete(_folder, true);

    [Theory]
    [InlineData(ImageFormat.Mono8)]
    [InlineData(ImageFormat.Mono16)]
    [InlineData(ImageFormat.Bgr24)]
    [InlineData(ImageFormat.Bgra32)]
    public async Task RealPng_PreservesOddWidthPixelsAndFormat_WithoutInputOrNativeLifetime(ImageFormat format)
    {
        var type = format switch
        {
            ImageFormat.Mono8 => MatType.CV_8UC1,
            ImageFormat.Mono16 => MatType.CV_16UC1,
            ImageFormat.Bgr24 => MatType.CV_8UC3,
            _ => MatType.CV_8UC4
        };
        byte[] bytes = format switch
        {
            ImageFormat.Mono8 => [0, 127, 255, 1, 128, 254],
            ImageFormat.Mono16 => [0, 0, 255, 255, 0, 128, 52, 18, 1, 0, 254, 255],
            ImageFormat.Bgr24 => [0, 0, 255, 0, 255, 0, 255, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
            _ => [0, 0, 255, 0, 0, 255, 0, 128, 255, 0, 0, 255, 1, 2, 3, 17, 4, 5, 6, 31, 7, 8, 9, 99]
        };
        var path = Write("中文 空格.png", type, bytes);
        var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new ImageFileOperation(path);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var output = Assert.IsType<ImageFileOutput>(result.Value);
        var image = output.Image;
        Assert.Equal(format, image.Format);
        Assert.Equal((3, 2, bytes.Length / 2), (image.Width, image.Height, image.Stride));
        Assert.Equal(bytes, image.Pixels.ToArray());
        Assert.Equal(path, output.FilePath);
        Assert.Equal(expectedHash, output.ContentSha256);
        var second = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, second.Status);
        Assert.NotEqual(image.FrameId, second.Value!.Image.FrameId);
        Assert.NotEqual(image.CoordinateSpaceId, second.Value.Image.CoordinateSpaceId);
        var detached = second.Value.Image.Pixels.ToArray();
        detached[0] = 42;
        Assert.Equal(bytes, image.Pixels.ToArray());
        await engine.ReleaseContextAsync(context, default);
        File.Delete(path);
        Assert.Equal(bytes, image.Pixels.ToArray());
        Assert.Equal(bytes, second.Value.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task Mono16Tiff_DoesNotNormalizeOrReduceBitDepth()
    {
        byte[] pixels = [0, 0, 1, 0, 255, 255, 0, 128, 52, 18, 255, 0];
        var path = Write("16bit.tiff", MatType.CV_16UC1, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new ImageFileOperation(path), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(ImageFormat.Mono16, result.Value!.Image.Format);
        Assert.Equal(pixels, result.Value.Image.Pixels.ToArray());
        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task RequestFreezesPath_EveryExecutionRereadsFile_AndOldPublishedPixelsRemainValid()
    {
        byte[] original = [0, 127, 255, 1, 128, 254];
        var configuredPath = Write("a.png", MatType.CV_8UC1, original);
        var request = new ImageFileOperation(configuredPath);
        configuredPath = Write("b.png", MatType.CV_8UC1, [9, 9, 9, 9, 9, 9]);
        Assert.NotEqual(configuredPath, request.FilePath);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(original, result.Value!.Image.Pixels.ToArray());
        Write("a.png", MatType.CV_8UC1, [0, 0, 0, 0, 0, 0]);
        var black = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, black.Status);
        Assert.Equal(new byte[6], black.Value!.Image.Pixels.ToArray());
        Assert.NotEqual(result.Value.ContentSha256, black.Value.ContentSha256);
        Assert.NotEqual(result.Value.Image.FrameId, black.Value.Image.FrameId);
        Assert.Equal(original, result.Value.Image.Pixels.ToArray());
        File.Delete(request.FilePath);
        var missing = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Failed, missing.Status);
        Assert.Null(missing.Value);
        await engine.ReleaseContextAsync(context, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TiffStorageIsExplicit_FloatPreservedAnd16BitColorRejected(bool color)
    {
        var path = Path.Combine(_folder, "storage.tiff");
        using (var mat = new Mat(2, 3, color ? MatType.CV_16UC3 : MatType.CV_32FC1, new Scalar(100, 200, 300)))
        {
            Assert.True(Cv2.ImEncode(".tiff", mat, out var encoded));
            File.WriteAllBytes(path, encoded);
        }

        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new ImageFileOperation(path), default);
        if (color)
        {
            Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
            Assert.Null(result.Value);
        }
        else
        {
            // 当前存储契约已明确支持Float32；不延续旧测试“所有浮点文件均拒绝”的过时限制。
            Assert.Equal(VisionExecutionStatus.Success, result.Status);
            Assert.Equal(ImageFormat.Float32, result.Value!.Image.Format);
            var expected = Enumerable.Repeat(100f, 6).SelectMany(BitConverter.GetBytes).ToArray();
            Assert.Equal(expected, result.Value.Image.Pixels.ToArray());
        }

        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task EmptyMissingAndCorruptFilesHaveNoOutput_AndImageConsumersStillRequireRealInput()
    {
        Assert.Throws<ArgumentException>(() => new ImageFileOperation(""));
        Assert.Throws<ArgumentException>(() => new ImageFileOperation("relative.png"));
        Assert.Throws<ArgumentNullException>(() => new ThresholdRangeOperation(null!, 0, 255));
        var path = Path.Combine(_folder, "missing.png");
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new ImageFileOperation(path);
        var missing = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Failed, missing.Status);
        Assert.Null(missing.Value);
        File.WriteAllBytes(path, []);
        var empty = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Failed, empty.Status);
        Assert.Null(empty.Value);
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        var corrupt = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Failed, corrupt.Status);
        Assert.Null(corrupt.Value);
        await engine.ReleaseContextAsync(context, default);
    }

    private string Write(string name, MatType type, byte[] pixels)
    {
        using var mat = new Mat(2, 3, type);
        Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
        Assert.True(Cv2.ImEncode(Path.GetExtension(name), mat, out var encoded));
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, encoded);
        return path;
    }
}
