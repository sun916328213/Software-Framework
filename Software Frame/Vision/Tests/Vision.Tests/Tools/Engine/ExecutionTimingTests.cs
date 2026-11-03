using Vision.Core.Diagnostics;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class ExecutionTimingTests
{
    [Fact]
    public async Task CompletedNoMatchAndValidationFailure_ReportMeasuredDuration()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var gray = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 1, PixelFormat.Mono8, [0, 0]);
        var success = await engine.ExecuteAsync(context, new ThresholdRangeOperation(gray, 0, 0), default);
        var empty = await engine.ExecuteAsync(context, new BlobOperation(gray, 255, 255), default);
        var invalid = await engine.ExecuteAsync(context, new ThresholdRangeOperation(gray, 0, 256), default);
        var color = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Bgr24, [0, 0, 0]);
        var unsupported = await engine.ExecuteAsync(context, new ThresholdRangeOperation(color, 0, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, success.Status);
        Assert.Equal(VisionExecutionStatus.NoMatch, empty.Status);
        Assert.Equal(VisionExecutionStatus.Failed, invalid.Status);
        Assert.Equal(VisionExecutionStatus.Unsupported, unsupported.Status);
        Assert.True(success.ExecutionDuration >= TimeSpan.Zero);
        Assert.True(empty.ExecutionDuration >= TimeSpan.Zero);
        Assert.True(invalid.ExecutionDuration >= TimeSpan.Zero);
        Assert.True(unsupported.ExecutionDuration >= TimeSpan.Zero);
    }

    [Fact]
    public void ExplicitOutcomeDuration_IsOptionalAndCannotBeNegative()
    {
        var issue = new VisionIssue("Failure", "No output");
        var absent = new VisionOutcome<BlobOutput>(VisionExecutionStatus.Failed, null, issue);
        Assert.Null(absent.ExecutionDuration);
        Assert.Throws<ArgumentOutOfRangeException>(() => new VisionOutcome<BlobOutput>(VisionExecutionStatus.Failed, null, issue, TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public async Task CancelledExecution_StillThrowsRatherThanReturningFakeDuration()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var image = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Mono8, [0]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.ExecuteAsync(context, new ThresholdRangeOperation(image, 0, 0), cancellation.Token).AsTask());
    }
}
