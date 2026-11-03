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
public sealed class QrCodeReadManyEngineTests
{
    [Fact]
    public async Task ActualTwoIdenticalPayloads_RemainTwoPositions_AndCountAndExplicitTextSelectionSurviveClose()
    {
        var frame = Scene();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadManyOperation(frame, 4), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.DetectedCount);
        Assert.Equal(2, result.Value.DecodedCount);
        Assert.All(result.Value.Candidates, candidate => Assert.Equal("VISION-42", candidate.Text));
        Assert.Equal(40, result.Value.Candidates[0].Corners.Min(p => p.X), 2);
        Assert.Equal(300, result.Value.Candidates[1].Corners.Min(p => p.X), 2);
        Assert.All(result.Value.Candidates, candidate =>
        {
            Assert.Equal(frame.FrameId, candidate.FrameId);
            Assert.Equal(frame.CoordinateSpaceId, candidate.CoordinateSpaceId);
        });
        await engine.ReleaseContextAsync(context, default);
        var evaluating = await engine.CreateContextAsync(default);
        var ok = await engine.ExecuteAsync(evaluating, new QrDecodedCountOperation(result.Value, 2, 2), default);
        var ng = await engine.ExecuteAsync(evaluating, new QrDecodedCountOperation(result.Value, 1, 1), default);
        var text = await engine.ExecuteAsync(evaluating, new QrTextEqualsOperation(result.Value.Candidates[1], "VISION-42"), default);
        Assert.Equal(VisionExecutionStatus.Success, ok.Status);
        Assert.Equal(2, ok.Value!.ActualCount);
        Assert.True(ok.Value.Passed);
        Assert.Same(result.Value, ok.Value.Source);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
        Assert.True(text.Value!.Passed);
    }

    [Fact]
    public async Task ActualMixedDecodedAndUndecodableCandidates_KeepIndependentGeometryWithoutFalseText()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadManyOperation(Scene(damageSecond: true)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.DetectedCount);
        Assert.Equal(1, result.Value.DecodedCount);
        Assert.Equal(QrCodeReadState.Decoded, result.Value.Candidates[0].State);
        Assert.Equal(QrCodeReadState.DetectedUndecodable, result.Value.Candidates[1].State);
        Assert.Null(result.Value.Candidates[1].Text);
        Assert.Equal(4, result.Value.Candidates[1].Corners.Count);
        var count = await engine.ExecuteAsync(context, new QrDecodedCountOperation(result.Value, 1, 1), default);
        Assert.True(count.Value!.Passed);
        var text = await engine.ExecuteAsync(context, new QrTextEqualsOperation(result.Value.Candidates[1], "VISION-42"), default);
        Assert.False(text.Value!.Passed);
    }

    [Fact]
    public async Task BlankScene_ReturnsNoMatchEmptySnapshot_ThenZeroDecodedCountPasses()
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 128, 128, PixelFormat.Mono8,
            Enumerable.Repeat((byte)255, 128 * 128).ToArray());
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadManyOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Empty(result.Value!.Candidates);
        Assert.Equal(0, result.Value.DecodedCount);
        var decision = await engine.ExecuteAsync(context, new QrDecodedCountOperation(result.Value, 0, 0), default);
        Assert.True(decision.Value!.Passed);
    }

    [Fact]
    public async Task ActualAllCandidatesUndecodable_IsNoMatchButPreservesDetectedGeometry()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadManyOperation(Scene(damageBoth: true)), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Equal(2, result.Value!.DetectedCount);
        Assert.Equal(0, result.Value.DecodedCount);
        Assert.All(result.Value.Candidates, candidate =>
        {
            Assert.Equal(QrCodeReadState.DetectedUndecodable, candidate.State);
            Assert.Null(candidate.Text);
            Assert.Equal(4, candidate.Corners.Count);
        });
        var decision = await engine.ExecuteAsync(context, new QrDecodedCountOperation(result.Value, 0, 0), default);
        Assert.True(decision.Value!.Passed);
    }

    [Fact]
    public async Task DetectedCandidatesOverBudget_FailWithoutTruncatingOrReportingFalseCount()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadManyOperation(Scene(), maximumCandidates: 1), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ExplicitWindow_OnlyReturnsItsCandidate_WithOriginalImageCoordinates()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadManyOperation(Scene(), 4, 268, 8, 232, 232), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var candidate = Assert.Single(result.Value!.Candidates);
        Assert.Equal(300, candidate.Corners.Min(p => p.X), 2);
        Assert.Equal(40, candidate.Corners.Min(p => p.Y), 2);
        Assert.Equal("VISION-42", candidate.Text);
    }

    [Fact]
    public void InvalidBudgetsWindowsCrossSourceOrDuplicateGeometry_AreRejected()
    {
        var frame = Scene();
        Assert.Throws<ArgumentOutOfRangeException>(() => new QrCodeReadManyOperation(frame, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QrCodeReadManyOperation(frame, 33));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QrCodeReadManyOperation(frame, 4, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QrCodeReadManyOperation(frame, 4, 0, 0, 20, 21));
        var first = Candidate(frame.FrameId, frame.CoordinateSpaceId, 0, true);
        var second = Candidate(frame.FrameId, frame.CoordinateSpaceId, 20, false);
        Assert.Throws<ArgumentException>(() => new QrCodeReadManyOutput(frame.FrameId, frame.CoordinateSpaceId, [first, first]));
        Assert.Throws<ArgumentException>(() => new QrCodeReadManyOutput(frame.FrameId, frame.CoordinateSpaceId, [second, first]));
        Assert.Throws<ArgumentException>(() => new QrCodeReadManyOutput(frame.FrameId, frame.CoordinateSpaceId,
            [Candidate(Guid.NewGuid(), frame.CoordinateSpaceId, 0, true)]));
        Assert.Throws<ArgumentException>(() => new QrCodeReadManyOutput(frame.FrameId, frame.CoordinateSpaceId,
            [new(frame.FrameId, frame.CoordinateSpaceId, QrCodeReadState.NotDetected, null, [])]));
        var input = new List<QrCodeReadOutput> { first, second };
        var snapshot = new QrCodeReadManyOutput(frame.FrameId, frame.CoordinateSpaceId, input);
        input.Clear();
        Assert.Equal(2, snapshot.DetectedCount);
        Assert.Equal(1, snapshot.DecodedCount);
    }

    [Fact]
    public async Task DecodedCountJudgment_IsNoSdk_AndUndecodableCandidatesAreNotSuccessfulReads()
    {
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => new BoundaryBackend(noCapabilities: true));
        var context = await engine.CreateContextAsync(default);
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        var source = new QrCodeReadManyOutput(frame, space, [Candidate(frame, space, 0, false), Candidate(frame, space, 20, false)]);
        var request = new QrDecodedCountOperation(source, 0, 0);
        Assert.Null(request.Image);
        var zero = await engine.ExecuteAsync(context, request, default);
        var ng = await engine.ExecuteAsync(context, new QrDecodedCountOperation(source, 2, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, zero.Status);
        Assert.True(zero.Value!.Passed);
        Assert.Equal(0, zero.Value.ActualCount);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
        Assert.Throws<ArgumentOutOfRangeException>(() => new QrDecodedCountOperation(source, -1, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrongNoMatchFlagOrOversizedBackendSnapshot_IsRejected(bool wrongNoMatch)
    {
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => new BoundaryBackend(wrongNoMatch: wrongNoMatch));
        var context = await engine.CreateContextAsync(default);
        var request = new QrCodeReadManyOperation(Scene(), maximumCandidates: wrongNoMatch ? 4 : 1);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Mono16IsUnsupported_NotImplicitlyNormalizedForMultiDetection()
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 32, 32, PixelFormat.Mono16, new byte[32 * 32 * 2]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new QrCodeReadManyOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Equal("UnsupportedPixelFormat", result.Issue!.Code);
    }

    private static QrCodeReadOutput Candidate(Guid frame, Guid space, double x, bool decoded)
        => new(frame, space, decoded ? QrCodeReadState.Decoded : QrCodeReadState.DetectedUndecodable,
            decoded ? "VISION-42" : null, [new(x, 0), new(x + 10, 0), new(x + 10, 10), new(x, 10)]);

    private static VisionFrame Scene(bool damageSecond = false, bool damageBoth = false)
    {
        var code = QrCodeReadEngineTests.Scene();
        var pixels = Enumerable.Repeat((byte)255, 520 * 256).ToArray();
        foreach (var offset in new[] { 8, 268 })
        {
            for (var row = 0; row < 232; row++)
            {
                code.Pixels.Slice(row * 232, 232).CopyTo(pixels.AsSpan((row + 8) * 520 + offset, 232));
            }
        }
        if (damageSecond || damageBoth)
        {
            // 固定6×6数据模块破坏：保留检测几何但数据不可解码；不把检测框当成功文本。
            foreach (var offset in damageBoth ? new[] { 8, 268 } : new[] { 268 })
            {
                for (var row = 32 + 9 * 8; row < 32 + 15 * 8; row++)
                {
                    pixels.AsSpan((row + 8) * 520 + offset + 32 + 9 * 8, 6 * 8).Fill(255);
                }
            }
        }
        return new(Guid.NewGuid(), Guid.NewGuid(), 520, 256, PixelFormat.Mono8, pixels);
    }

    private sealed class BoundaryBackend(bool noCapabilities = false, bool wrongNoMatch = false) : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => noCapabilities ? []
            : [new(QrCodeReadManyOperation.Id, Domain, VisionOperationFeatures.MultipleMatches)];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
        {
            if (noCapabilities)
            {
                throw new InvalidOperationException("QR count must not use SDK validation");
            }
            return [];
        }
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
        {
            if (noCapabilities)
            {
                throw new InvalidOperationException("QR count must not use SDK execution");
            }
            var frame = operation.Image!;
            var output = new QrCodeReadManyOutput(frame.FrameId, frame.CoordinateSpaceId,
                [Candidate(frame.FrameId, frame.CoordinateSpaceId, 0, true), Candidate(frame.FrameId, frame.CoordinateSpaceId, 20, true)]);
            return ValueTask.FromResult(new BackendOutput(output, wrongNoMatch));
        }
        public void Dispose()
        {
        }
    }
}
