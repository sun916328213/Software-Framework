using System.Buffers.Binary;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

/// <summary>保留原Otsu解析规则；旧Mode/JSON/动态数值绑定不作为新契约兼容目标。</summary>
[Trait("Category", "Regression")]
public sealed class OtsuThresholdTests
{
    [Theory]
    [InlineData(false, 0, 80)]
    [InlineData(true, 0, 80)]
    [InlineData(false, 10, 200)]
    [InlineData(true, 10, 200)]
    public async Task TwoLevelImage_UsesSmallestOtsuThresholdAndStrictBinaryRule(bool inverse, byte low, byte high)
    {
        byte[] pixels = [low, low, high, high];
        var image = Frame(pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new OtsuThresholdOperation(image, inverse), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(low, result.Value!.Threshold);
        Assert.Equal(inverse ? new byte[] { 255, 255, 0, 0 } : [0, 0, 255, 255], result.Value.Image.Pixels.ToArray());
        Assert.Equal(2, CountWhite(result.Value.Image));
        Assert.Equal(0.5, CountWhite(result.Value.Image) / 4d);
        Assert.Equal(new byte[] { low, low, high, high }, pixels);
        Assert.Equal(image.FrameId, result.Value.FrameId);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 4)]
    [InlineData(false, 7, 4)]
    [InlineData(true, 7, 0)]
    [InlineData(false, 255, 4)]
    [InlineData(true, 255, 0)]
    public async Task ConstantImage_HasValidZeroThreshold_NotNoMatchOrFakeFailure(bool inverse, byte gray, int count)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new OtsuThresholdOperation(Frame([gray, gray, gray, gray]), inverse), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(0, result.Value!.Threshold);
        Assert.Equal(count, CountWhite(result.Value.Image));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("color")]
    [InlineData("16bit")]
    public async Task InputKindsAreExplicit_AndSupported16BitKeepsRawThreshold(string kind)
    {
        if (kind == "missing")
        {
            Assert.Throws<ArgumentNullException>(() => new OtsuThresholdOperation(null!));
            return;
        }

        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        if (kind == "color")
        {
            var color = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Bgr24, [0, 0, 0]);
            var unsupported = await engine.ExecuteAsync(context, new OtsuThresholdOperation(color), default);
            Assert.Equal(VisionExecutionStatus.Unsupported, unsupported.Status);
            Assert.Null(unsupported.Value);
            return;
        }

        // 新契约已支持原位深Mono16，不保留旧用例“一概拒绝16位”的过时限制。
        ushort[] values = [1000, 1000, 50000, 50000];
        var bytes = new byte[8];
        for (var index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2, 2), values[index]);
        }

        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Mono16, bytes);
        var result = await engine.ExecuteAsync(context, new OtsuThresholdOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(1000, result.Value!.Threshold);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, result.Value.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task AutomaticAndExplicitRangeAreDistinctRequests_NotSilentlyMixedNumericBindings()
    {
        var image = Frame([0, 80, 80, 120]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var range = await engine.ExecuteAsync(context, new ThresholdRangeOperation(image, 80, 80), default);
        var otsu = await engine.ExecuteAsync(context, new OtsuThresholdOperation(image), default);
        Assert.Equal(VisionExecutionStatus.Success, range.Status);
        Assert.Equal(VisionExecutionStatus.Success, otsu.Status);
        Assert.Equal(new byte[] { 0, 255, 255, 0 }, range.Value!.Image.Pixels.ToArray());
        Assert.Equal(new byte[] { 0, 255, 255, 255 }, otsu.Value!.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task PaddedPixelsAndInverseAreFrozen_AndPublishedSnapshotOutlivesContext()
    {
        byte[] pixels = [0, 80, 99, 80, 0, 99];
        var inverse = true;
        var image = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Mono8, pixels, 3);
        var request = new OtsuThresholdOperation(image, inverse);
        inverse = false;
        Array.Clear(pixels);
        Assert.NotEqual(inverse, request.Inverse);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(0, result.Value.Threshold);
    }

    [Fact]
    public async Task InvalidRangeCannotContaminateAutomaticThreshold_WhichHasNoRangeParameters()
    {
        var image = Frame([0, 0, 0, 0]);
        Assert.Throws<ArgumentException>(() => new ThresholdRangeOperation(image, 255, 0));
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var automatic = await engine.ExecuteAsync(context, new OtsuThresholdOperation(image), default);
        Assert.Equal(VisionExecutionStatus.Success, automatic.Status);
        Assert.Equal(0, automatic.Value!.Threshold);
    }

    [Fact]
    public async Task ExplicitInclusiveRangeDoesNotDefaultToOtsu_OrRestoreLegacyModeJson()
    {
        var image = Frame([0, 80, 80, 255]);
        var range = new ThresholdRangeOperation(image, 80, 80);
        var automatic = new OtsuThresholdOperation(image);
        Assert.NotEqual(range.OperationId, automatic.OperationId);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, range, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 0, 255, 255, 0 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(80, range.Minimum);
        Assert.Equal(80, range.Maximum);
    }

    private static VisionFrame Frame(byte[] pixels) => new(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Mono8, pixels);
    private static int CountWhite(VisionFrame image)
    {
        var count = 0;
        for (var row = 0; row < image.Height; row++)
        {
            foreach (var pixel in image.Pixels.Slice(row * image.Stride, image.Width))
            {
                if (pixel == 255)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
