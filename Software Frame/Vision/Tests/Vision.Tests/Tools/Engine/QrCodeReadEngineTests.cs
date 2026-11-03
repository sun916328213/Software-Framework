using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Vision.Tools.Execution;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class QrCodeReadEngineTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ActualQr_DecodesTextAndSourceCorners_ThenExactJudgmentWorksAfterContextRelease(int quarterTurns)
    {
        var frame = Scene(quarterTurns: quarterTurns);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(QrCodeReadState.Decoded, result.Value!.State);
        Assert.Equal("VISION-42", result.Value.Text);
        Assert.Equal(1, result.Value.DecodedCount);
        Assert.Equal(1, result.Value.DetectedCount);
        Assert.Equal(frame.FrameId, result.Value.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, result.Value.CoordinateSpaceId);
        AssertCorners(result.Value, 32, 32, 199, 199);
        await engine.ReleaseContextAsync(context, default);
        var evaluating = await engine.CreateContextAsync(default);
        var ok = await engine.ExecuteAsync(evaluating, new QrTextEqualsOperation(result.Value, "VISION-42"), default);
        var ng = await engine.ExecuteAsync(evaluating, new QrTextEqualsOperation(result.Value, "vision-42"), default);
        Assert.Equal(VisionExecutionStatus.Success, ok.Status);
        Assert.True(ok.Value!.Passed);
        Assert.Same(result.Value, ok.Value.Source);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
    }

    [Fact]
    public async Task ExplicitWindow_OffsetsNativeCornersBackToOriginalImage()
    {
        var frame = Scene(offsetX: 40, offsetY: 20, width: 300, height: 280);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadOperation(frame, 40, 20, 232, 232), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        AssertCorners(result.Value!, 72, 52, 239, 219);
        Assert.Equal(frame.FrameId, result.Value!.FrameId);
        Assert.Equal("VISION-42", result.Value.Text);
    }

    [Fact]
    public async Task BlankImage_IsNotDetectedNoMatch_WithEmptySnapshot_NotExecutionError()
    {
        var frame = Blank(256, 256);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Equal(QrCodeReadState.NotDetected, result.Value!.State);
        Assert.Null(result.Value.Text);
        Assert.Empty(result.Value.Corners);
        Assert.Equal(0, result.Value.DecodedCount);
        Assert.Equal(0, result.Value.DetectedCount);
    }

    [Fact]
    public async Task DamagedData_IsDetectedButUndecodable_NotFalseDecodedText()
    {
        var frame = Scene(damaged: true);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Equal(QrCodeReadState.DetectedUndecodable, result.Value!.State);
        Assert.Equal(1, result.Value.DetectedCount);
        Assert.Equal(0, result.Value.DecodedCount);
        Assert.Null(result.Value.Text);
        AssertCorners(result.Value, 32, 32, 199, 199);
        var decision = await engine.ExecuteAsync(context, new QrTextEqualsOperation(result.Value, "VISION-42"), default);
        Assert.Equal(VisionExecutionStatus.Success, decision.Status);
        Assert.False(decision.Value!.Passed);
    }

    [Fact]
    public async Task SearchWindow_DoesNotDecodeCodeOutsideItsBounds()
    {
        var frame = Scene(offsetX: 20, offsetY: 40, width: 600, height: 300);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadOperation(frame, 320, 0, 240, 260), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Equal(QrCodeReadState.NotDetected, result.Value!.State);
        Assert.Empty(result.Value.Corners);
    }

    [Theory]
    [InlineData(PixelFormat.Mono16, 2)]
    [InlineData(PixelFormat.Rgb24, 3)]
    public async Task OtherFormats_AreUnsupported_WithoutImplicitGrayOrDepthMapping(PixelFormat format, int bytes)
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 32, 32, format, new byte[32 * 32 * bytes]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Equal("UnsupportedPixelFormat", result.Issue!.Code);
        Assert.Null(result.Value);
    }

    [Fact]
    public void InvalidWindowsAndInconsistentTextOrCorners_AreRejected()
    {
        var frame = Blank(256, 256);
        Assert.Throws<ArgumentOutOfRangeException>(() => new QrCodeReadOperation(frame, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QrCodeReadOperation(frame, 0, 0, 20, 256));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QrCodeReadOperation(frame, 250, 0, 21, 21));
        Point2D[] corners = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        Assert.Throws<ArgumentException>(() => new QrCodeReadOutput(frame.FrameId, frame.CoordinateSpaceId, QrCodeReadState.Decoded, null, corners));
        Assert.Throws<ArgumentException>(() => new QrCodeReadOutput(frame.FrameId, frame.CoordinateSpaceId, QrCodeReadState.NotDetected, null, corners));
        Assert.Throws<ArgumentException>(() => new QrCodeReadOutput(frame.FrameId, frame.CoordinateSpaceId, QrCodeReadState.DetectedUndecodable, "old", corners));
        Assert.Throws<ArgumentException>(() => new QrCodeReadOutput(frame.FrameId, frame.CoordinateSpaceId, QrCodeReadState.Decoded, "text",
            [new(0, 0), new(1, 0), new(2, 0), new(3, 0)]));
    }

    [Fact]
    public async Task ExactTextJudgment_IsTrulyNoSdk_AndDoesNotTrimOrTreatMissingCodeAsSuccess()
    {
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => new QrBoundaryBackend(noCapabilities: true));
        var context = await engine.CreateContextAsync(default);
        var source = new QrCodeReadOutput(Guid.NewGuid(), Guid.NewGuid(), QrCodeReadState.Decoded, "VISION-42",
            [new(0, 0), new(10, 0), new(10, 10), new(0, 10)]);
        var request = new QrTextEqualsOperation(source, "VISION-42");
        Assert.Null(request.Image);
        var ok = await engine.ExecuteAsync(context, request, default);
        var whitespace = await engine.ExecuteAsync(context, new QrTextEqualsOperation(source, "VISION-42 "), default);
        var absent = new QrCodeReadOutput(Guid.NewGuid(), Guid.NewGuid(), QrCodeReadState.NotDetected, null, []);
        var missing = await engine.ExecuteAsync(context, new QrTextEqualsOperation(absent, "VISION-42"), default);
        Assert.Equal(VisionExecutionStatus.Success, ok.Status);
        Assert.True(ok.Value!.Passed);
        Assert.False(whitespace.Value!.Passed);
        Assert.Equal(VisionExecutionStatus.Success, missing.Status);
        Assert.False(missing.Value!.Passed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackendExceptionOrWrongSource_IsFailed_NotNoMatch(bool throwNative)
    {
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => new QrBoundaryBackend(throwNative: throwNative));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadOperation(Blank(32, 32)), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task WrongCompletionFlagOrWindowCoordinates_IsRejected(int fault)
    {
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => new QrBoundaryBackend(resultFault: fault));
        var context = await engine.CreateContextAsync(default);
        var image = Blank(64, 64);
        var request = fault == 2 ? new QrCodeReadOperation(image, 20, 20, 32, 32) : new QrCodeReadOperation(image);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    private static void AssertCorners(QrCodeReadOutput output, double minimumX, double minimumY, double maximumX, double maximumY)
    {
        Assert.Equal(4, output.Corners.Count);
        Assert.Equal(minimumX, output.Corners.Min(point => point.X), 2);
        Assert.Equal(minimumY, output.Corners.Min(point => point.Y), 2);
        Assert.Equal(maximumX, output.Corners.Max(point => point.X), 2);
        Assert.Equal(maximumY, output.Corners.Max(point => point.Y), 2);
    }

    // 固定版本1模块矩阵，来自独立编码器对公开ASCII文本VISION-42的生成结果，实际OpenCV原生已交叉解码。
    // 测试运行离线，不请求网络，也不使用生产解码器反造期望结果。21模块，4模块静区，每模块8像素。
    private static readonly string[] Modules =
    [
        "111111100011101111111", "100000101110101000001", "101110100011101011101",
        "101110101100101011101", "101110100100101011101", "100000101001001000001",
        "111111101010101111111", "000000000100000000000", "111110111001010101010",
        "110000011011111100010", "010011100100101101110", "010001001001111101000",
        "100100110010100101001", "000000001100100101010", "111111101011010001111",
        "100000100110000100100", "101110101111010111110", "101110101001111100100",
        "101110101100101011000", "100000101011111001001", "111111101100100111000"
    ];

    internal static VisionFrame Scene(int quarterTurns = 0, bool damaged = false, int offsetX = 0, int offsetY = 0,
        int width = 232, int height = 232)
    {
        var pixels = Enumerable.Repeat((byte)255, width * height).ToArray();
        for (var moduleY = 0; moduleY < 21; moduleY++)
        {
            for (var moduleX = 0; moduleX < 21; moduleX++)
            {
                if (Modules[moduleY][moduleX] != '1' || damaged && moduleX >= 9 && moduleY >= 9)
                {
                    continue;
                }
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        var sourceX = 32 + moduleX * 8 + x;
                        var sourceY = 32 + moduleY * 8 + y;
                        var targetX = quarterTurns == 1 ? 231 - sourceY : sourceX;
                        var targetY = quarterTurns == 1 ? sourceX : sourceY;
                        pixels[(offsetY + targetY) * width + offsetX + targetX] = 0;
                    }
                }
            }
        }
        return new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);
    }

    private static VisionFrame Blank(int width, int height) => new(Guid.NewGuid(), Guid.NewGuid(), width, height,
        PixelFormat.Mono8, Enumerable.Repeat((byte)255, width * height).ToArray());

    private sealed class QrBoundaryBackend(bool noCapabilities = false, bool throwNative = false, int resultFault = 0) : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => noCapabilities ? [] : [new(QrCodeReadOperation.Id, Domain, VisionOperationFeatures.None)];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
        {
            if (noCapabilities)
            {
                throw new InvalidOperationException("Text judgment must not call SDK validation");
            }
            return [];
        }
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
        {
            if (noCapabilities || throwNative)
            {
                throw new InvalidOperationException("Simulated native error must not become empty QR NoMatch");
            }
            return ValueTask.FromResult(new BackendOutput(new QrCodeReadOutput(
                resultFault == 0 ? Guid.NewGuid() : operation.Image!.FrameId, operation.Image!.CoordinateSpaceId,
                QrCodeReadState.Decoded, "boundary-test", [new(1, 1), new(10, 1), new(10, 10), new(1, 10)]), resultFault == 1));
        }
        public void Dispose()
        {
        }
    }
}
