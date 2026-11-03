using System.Buffers.Binary;
using System.Security.Cryptography;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Vision.Tools.Execution;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class ImageIoConversionTests
{
    // 按PNG规范独立构造的固定像素fixture，不依靠被测SDK编码器生成期望值。
    private const string Gray16Png = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACEAAAAAAHTY67AAAAFUlEQVR4AQEKAPX/AAAAA+gAgAD//wqMA2q8I4cPAAAAAElFTkSuQmCC";
    private const string RgbPng = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAIAAAB7QOjdAAAAEklEQVR4AQEHAPj/AP8AAAAA/wcAAf9VNrrHAAAAAElFTkSuQmCC";

    [Fact]
    public async Task FileRead_Preserves16Bit_ReportsActualContent_AndCreatesNewIdentityEachTime()
    {
        using var file = new ImageFile(Gray16Png);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new ImageFileOperation(file.Path);
        Assert.Null(request.Image);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var read = result.Value!;
        Assert.Equal(PixelFormat.Mono16, read.Image.Format);
        Assert.Equal(new ushort[] { 0, 1000, 32768, 65535 }, Values16(read.Image));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(Gray16Png))).ToLowerInvariant(), read.ContentSha256);
        Assert.Equal(file.Path, read.FilePath);
        var again = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, again.Status);
        Assert.NotEqual(read.Image.FrameId, again.Value!.Image.FrameId);
        Assert.NotEqual(read.Image.CoordinateSpaceId, again.Value.Image.CoordinateSpaceId);
    }

    [Fact]
    public async Task ColourFile_UsesDeclaredBgrOrder_AndExplicitGrayConversionFeedsStatistics()
    {
        using var file = new ImageFile(RgbPng);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var read = await engine.ExecuteAsync(context, new ImageFileOperation(file.Path), default);
        Assert.Equal(VisionExecutionStatus.Success, read.Status);
        var image = read.Value!.Image;
        Assert.Equal(PixelFormat.Bgr24, image.Format);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0 }, image.Pixels.ToArray());
        var gray = await engine.ExecuteAsync(context, new ImageConvertOperation(image, PixelFormat.Mono8), default);
        Assert.Equal(VisionExecutionStatus.Success, gray.Status);
        Assert.Equal(new byte[] { 76, 29 }, gray.Value!.Image.Pixels.ToArray());
        Assert.Equal(image.FrameId, gray.Value.FrameId);
        Assert.NotEqual(image.FrameId, gray.Value.Image.FrameId);
        Assert.Equal(image.CoordinateSpaceId, gray.Value.Image.CoordinateSpaceId);
        var measured = await engine.ExecuteAsync(context, new GrayStatisticsOperation(gray.Value.Image), default);
        Assert.Equal(VisionExecutionStatus.Success, measured.Status);
        Assert.Equal(52.5, measured.Value!.Mean);
    }

    [Fact]
    public async Task ColourConversion_RespectsRgbBgrAndPadding_AndDropsAlphaWithoutCompositing()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var rgb = Frame(2, 1, PixelFormat.Rgb24, [255, 0, 0, 0, 0, 255, 91, 92], 8);
        var swapped = await engine.ExecuteAsync(context, new ImageConvertOperation(rgb, PixelFormat.Bgr24), default);
        Assert.Equal(VisionExecutionStatus.Success, swapped.Status);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0 }, swapped.Value!.Image.Pixels.ToArray());
        var gray = await engine.ExecuteAsync(context, new ImageConvertOperation(rgb, PixelFormat.Mono8), default);
        Assert.Equal(new byte[] { 76, 29 }, gray.Value!.Image.Pixels.ToArray());
        var bgra = Frame(2, 1, PixelFormat.Bgra32, [0, 0, 255, 0, 255, 0, 0, 255]);
        var opaque = await engine.ExecuteAsync(context, new ImageConvertOperation(bgra, PixelFormat.Bgr24), default);
        Assert.Equal(VisionExecutionStatus.Success, opaque.Status);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0 }, opaque.Value!.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task LinearHalfUp_IsExplicitAndDoesNotChangeExistingSdkRounding()
    {
        var input = Frame(3, 2, PixelFormat.Mono8, [0, 5, 10, 77, 128, 200, 255, 77], 4);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var defaultResult = await engine.ExecuteAsync(context, new GrayLinearOperation(input, 0.5, 0, PixelFormat.Mono8), default);
        var halfUp = await engine.ExecuteAsync(context, new GrayLinearOperation(input, 0.5, 0, PixelFormat.Mono8, GrayLinearRounding.HalfUp), default);
        Assert.Equal(VisionExecutionStatus.Success, defaultResult.Status);
        Assert.Equal(VisionExecutionStatus.Success, halfUp.Status);
        Assert.Equal(2, defaultResult.Value!.Image.Pixels[1]);
        Assert.Equal(new byte[] { 0, 3, 5, 64, 100, 128 }, halfUp.Value!.Image.Pixels.ToArray());
        var inverted = await engine.ExecuteAsync(context, new GrayLinearOperation(input, -1, 255, PixelFormat.Mono8, GrayLinearRounding.HalfUp), default);
        Assert.Equal(new byte[] { 255, 250, 245, 127, 55, 0 }, inverted.Value!.Image.Pixels.ToArray());
        Assert.Equal(new byte[] { 0, 5, 10, 77, 128, 200, 255, 77 }, input.Pixels.ToArray());
    }

    [Fact]
    public async Task LinearHalfUp_DoesNotPromoteTheDoubleImmediatelyBelowTheMidpoint()
    {
        var input = Frame(1, 1, PixelFormat.Mono8, [0]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var below = await engine.ExecuteAsync(context, new GrayLinearOperation(input, 0, Math.BitDecrement(0.5), PixelFormat.Mono8, GrayLinearRounding.HalfUp), default);
        var tie = await engine.ExecuteAsync(context, new GrayLinearOperation(input, 0, 0.5, PixelFormat.Mono8, GrayLinearRounding.HalfUp), default);
        var above = await engine.ExecuteAsync(context, new GrayLinearOperation(input, 0, Math.BitIncrement(0.5), PixelFormat.Mono8, GrayLinearRounding.HalfUp), default);
        Assert.Equal(VisionExecutionStatus.Success, below.Status);
        Assert.Equal(VisionExecutionStatus.Success, tie.Status);
        Assert.Equal(VisionExecutionStatus.Success, above.Status);
        Assert.Equal(0, below.Value!.Image.Pixels[0]);
        Assert.Equal(1, tie.Value!.Image.Pixels[0]);
        Assert.Equal(1, above.Value!.Image.Pixels[0]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LinearHalfUp_PreservesExplicitOutputDepthAndUsesRawValues(bool input16, bool output16)
    {
        byte[] bytes = input16 ? [5, 0, 255, 255, 73, 73] : [5, 255, 73, 73];
        var input = Frame(2, 1, input16 ? PixelFormat.Mono16 : PixelFormat.Mono8, bytes, bytes.Length);
        var format = output16 ? PixelFormat.Mono16 : PixelFormat.Mono8;
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayLinearOperation(input, 0.5, 0, format, GrayLinearRounding.HalfUp), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(format, result.Value!.Image.Format);
        var expectedSecond = input16 ? (output16 ? 32768 : 255) : 128;
        if (output16)
        {
            Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16LittleEndian(result.Value.Image.Pixels));
            Assert.Equal((ushort)expectedSecond, BinaryPrimitives.ReadUInt16LittleEndian(result.Value.Image.Pixels.Slice(2)));
        }
        else
        {
            Assert.Equal(new byte[] { 3, (byte)expectedSecond }, result.Value.Image.Pixels.ToArray());
        }
    }

    [Fact]
    public async Task IntegerWidening_PreservesValues_InsteadOfGuessingFullScaleNormalization()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var input = Frame(3, 1, PixelFormat.Mono8, [0, 128, 255, 73], 4);
        var result = await engine.ExecuteAsync(context, new ImageConvertOperation(input, PixelFormat.Mono16), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new ushort[] { 0, 128, 255 }, Values16(result.Value!.Image));
    }

    [Fact]
    public async Task GrayLinear_RequiresExplicitGainOffset_AndClampsBeforeOutputConversion()
    {
        using var file = new ImageFile(Gray16Png);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var read = await engine.ExecuteAsync(context, new ImageFileOperation(file.Path), default);
        Assert.Equal(VisionExecutionStatus.Success, read.Status);
        var source = read.Value!.Image;
        var before = source.Pixels.ToArray();
        var mapped = await engine.ExecuteAsync(context, new GrayLinearOperation(source, 255d / 65535, 0, PixelFormat.Mono8), default);
        Assert.Equal(VisionExecutionStatus.Success, mapped.Status);
        Assert.Equal(new byte[] { 0, 4, 128, 255 }, mapped.Value!.Image.Pixels.ToArray());
        var inverted = await engine.ExecuteAsync(context, new GrayLinearOperation(source, -1, 1000, PixelFormat.Mono16), default);
        Assert.Equal(VisionExecutionStatus.Success, inverted.Status);
        Assert.Equal(new ushort[] { 1000, 0, 0, 0 }, Values16(inverted.Value!.Image));
        var huge = await engine.ExecuteAsync(context, new GrayLinearOperation(source, 1e100, -1e50, PixelFormat.Mono8), default);
        Assert.Equal(VisionExecutionStatus.Success, huge.Status);
        Assert.Equal(new byte[] { 0, 255, 255, 255 }, huge.Value!.Image.Pixels.ToArray());
        Assert.Equal(before, source.Pixels.ToArray());
    }

    [Fact]
    public async Task CorruptOrMissingFile_AndUnspecifiedNarrowing_DoNotReturnOldDataOrGuessRanges()
    {
        using var file = new ImageFile(Gray16Png);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var original = await engine.ExecuteAsync(context, new ImageFileOperation(file.Path), default);
        Assert.Equal(VisionExecutionStatus.Success, original.Status);
        var rejected = await engine.ExecuteAsync(context, new ImageConvertOperation(original.Value!.Image, PixelFormat.Mono8), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, rejected.Status);
        Assert.Null(rejected.Value);
        File.WriteAllBytes(file.Path, [1, 2, 3]);
        var corrupt = await engine.ExecuteAsync(context, new ImageFileOperation(file.Path), default);
        Assert.Equal(VisionExecutionStatus.Failed, corrupt.Status);
        Assert.Null(corrupt.Value);
        var missing = await engine.ExecuteAsync(context, new ImageFileOperation(file.Path + ".missing"), default);
        Assert.Equal(VisionExecutionStatus.Failed, missing.Status);
        Assert.Null(missing.Value);
    }

    [Fact]
    public void InvalidConversionAndUnstableFileLocation_AreRejectedWithoutSdkWork()
    {
        var input = Frame(1, 1, PixelFormat.Mono8, [1]);
        Assert.Throws<ArgumentException>(() => new ImageFileOperation("relative.png"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageConvertOperation(input, PixelFormat.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayLinearOperation(input, double.NaN, 0, PixelFormat.Mono8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayLinearOperation(input, double.MaxValue, 0, PixelFormat.Mono8));
    }

    [Fact]
    public async Task FilePublication_RejectsAResultForAnotherPath()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".png");
        var backend = new WrongFileBackend(path + ".other");
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new ImageFileOperation(path), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Contains("文件来源", result.Issue!.Message);
    }

    private sealed class WrongFileBackend(string path) : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [new(ImageFileOperation.Id, Domain, VisionOperationFeatures.None)];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
            => ValueTask.FromResult(new BackendOutput(new ImageFileOutput(Frame(1, 1, PixelFormat.Mono8, [0]), path, new string('a', 64))));
        public void Dispose() { }
    }

    private sealed class ImageFile : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".png");
        internal ImageFile(string png) => File.WriteAllBytes(Path, Convert.FromBase64String(png));
        public void Dispose() => File.Delete(Path);
    }

    private static VisionFrame Frame(int width, int height, PixelFormat format, byte[] pixels, int stride = 0)
        => new(Guid.NewGuid(), Guid.NewGuid(), width, height, format, pixels, stride);

    private static ushort[] Values16(VisionFrame image)
    {
        var values = new ushort[image.Width * image.Height];
        for (var row = 0; row < image.Height; row++)
        {
            for (var column = 0; column < image.Width; column++)
            {
                values[row * image.Width + column] = BinaryPrimitives.ReadUInt16LittleEndian(image.Pixels.Slice(row * image.Stride + column * 2, 2));
            }
        }
        return values;
    }
}
