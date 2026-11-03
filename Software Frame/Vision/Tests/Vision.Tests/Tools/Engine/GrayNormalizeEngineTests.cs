using System.Buffers.Binary;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

public sealed class GrayNormalizeEngineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Range_UsesWholeFrameAndIgnoresPadding(bool sixteen)
    {
        var image = Frame(sixteen, [20, 30, 40]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayNormalizeOperation(image), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(20, result.Value!.InputMinimum);
        Assert.Equal(40, result.Value.InputMaximum);
        Assert.Equal(new ushort[] { 0, sixteen ? (ushort)32768 : (ushort)128, sixteen ? ushort.MaxValue : (ushort)255 }, Read(result.Value.Image));
        Assert.Equal(image.FrameId, result.Value.FrameId);
        Assert.NotEqual(image.FrameId, result.Value.Image.FrameId);
        Assert.Equal(image.CoordinateSpaceId, result.Value.Image.CoordinateSpaceId);
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(new ushort[] { 20, 30, 40 }, Read(image));
        Assert.Equal(0, Read(result.Value.Image)[0]);
    }

    [Theory]
    [InlineData(false, 123)]
    [InlineData(true, 40000)]
    public async Task ConstantFrame_RemainsUnchanged(bool sixteen, ushort value)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayNormalizeOperation(Frame(sixteen, [value, value])), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(value, result.Value!.InputMinimum);
        Assert.Equal(value, result.Value.InputMaximum);
        Assert.Equal(new ushort[] { value, value }, Read(result.Value.Image));
    }

    [Theory]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Float32, 4)]
    public async Task OtherFormats_AreUnsupported(PixelFormat format, int bytes)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var image = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, format, new byte[bytes]);
        var result = await engine.ExecuteAsync(context, new GrayNormalizeOperation(image), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task MultipleRows_UseOneGlobalRangeAndIgnoreEveryPaddingByte()
    {
        var image = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Mono8, [20, 30, 255, 30, 40, 0], 3);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayNormalizeOperation(image), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(20, result.Value!.InputMinimum);
        Assert.Equal(40, result.Value.InputMaximum);
        Assert.Equal(new byte[] { 0, 128, 128, 255 }, result.Value.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task Halcon_DoesNotClaimOrFallback()
    {
        await using var engine = VisionEngineFactory.Create(new("halcon"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayNormalizeOperation(Frame(false, [20, 40])), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
    }

    [Fact]
    public async Task NormalizedImage_CanFeedThresholdAndBlob()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var normalized = await engine.ExecuteAsync(context, new GrayNormalizeOperation(Frame(false, [20, 30, 40])), default);
        Assert.Equal(VisionExecutionStatus.Success, normalized.Status);
        var binary = await engine.ExecuteAsync(context, new ThresholdRangeOperation(normalized.Value!.Image, 128, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, binary.Status);
        var blobs = await engine.ExecuteAsync(context, new BlobOperation(binary.Value!.Image, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, blobs.Status);
        Assert.Equal(2, Assert.Single(blobs.Value!.Items).Area);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task BackendResultWithWrongFrameSpaceSizeOrFormat_IsNotPublished(int corruption)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"), (_, _) => new InvalidNormalizeBackend(corruption));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayNormalizeOperation(Frame(false, [1])), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task OversizedImage_IsRejectedBeforeNormalization()
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 20_000_001, 1, PixelFormat.Mono8, new byte[20_000_001]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var operation = new GrayNormalizeOperation(frame);
        Assert.Contains(await engine.ValidateAsync(context, operation, default), issue => issue.Code == "ImageBudgetExceeded");
        var result = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Result_RejectsInvalidExtrema()
    {
        var output = Frame(false, [1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayNormalizeOutput(Guid.NewGuid(), output, 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayNormalizeOutput(Guid.NewGuid(), output, 0, 256));
    }

    private sealed class InvalidNormalizeBackend(int corruption) : Vision.Tools.Execution.IVisionBackend
    {
        public string Id => "opencv";
        public Vision.Core.Capabilities.VisionExecutionDomain Domain => Vision.Core.Capabilities.VisionExecutionDomain.Vision;
        public IReadOnlyList<Vision.Core.Capabilities.VisionOperationCapability> Capabilities { get; } =
            [new(GrayNormalizeOperation.Id, Vision.Core.Capabilities.VisionExecutionDomain.Vision, Vision.Core.Capabilities.VisionOperationFeatures.None)];
        public IReadOnlyList<Vision.Core.Diagnostics.VisionIssue> Validate(Vision.Core.Engine.VisionOperation operation, Vision.Tools.Execution.IPreparedVisionModel? model) => [];
        public ValueTask<Vision.Tools.Execution.IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<Vision.Tools.Execution.BackendOutput> ExecuteAsync(Vision.Tools.Execution.IPreparedVisionModel? model, Vision.Core.Engine.VisionOperation operation, CancellationToken token)
        {
            var input = operation.Image!;
            var format = corruption == 3 ? PixelFormat.Mono16 : PixelFormat.Mono8;
            var width = corruption == 2 ? 2 : 1;
            var image = new VisionFrame(Guid.NewGuid(), corruption == 1 ? Guid.NewGuid() : input.CoordinateSpaceId, width, 1, format, new byte[corruption is 2 or 3 ? 2 : 1]);
            var output = new GrayNormalizeOutput(corruption == 0 ? Guid.NewGuid() : input.FrameId, image, 1, 1);
            return new(new Vision.Tools.Execution.BackendOutput(output));
        }
        public void Dispose()
        {
        }
    }

    private static VisionFrame Frame(bool sixteen, ushort[] values)
    {
        var bpp = sixteen ? 2 : 1;
        var data = Enumerable.Repeat((byte)99, values.Length * bpp + 2).ToArray();
        for (var i = 0; i < values.Length; i++)
        {
            if (sixteen)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i * 2, 2), values[i]);
            }
            else
            {
                data[i] = checked((byte)values[i]);
            }
        }
        return new(Guid.NewGuid(), Guid.NewGuid(), values.Length, 1, sixteen ? PixelFormat.Mono16 : PixelFormat.Mono8, data, data.Length);
    }

    private static ushort[] Read(VisionFrame frame)
    {
        var result = new ushort[frame.Width];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = frame.Format == PixelFormat.Mono16
                ? BinaryPrimitives.ReadUInt16LittleEndian(frame.Pixels.Slice(i * 2, 2)) : frame.Pixels[i];
        }
        return result;
    }
}
