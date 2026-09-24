using CommunicationNew.Runtime;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

public sealed class BoundedOperationTrackerTests
{
    [Fact]
    public async Task Tracker_rejects_work_above_capacity_and_accepts_again_after_completion()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tracker = new BoundedOperationTracker(2, _ => { });

        Assert.True(tracker.TryStart(() => release.Task));
        Assert.True(tracker.TryStart(() => release.Task));
        Assert.False(tracker.TryStart(() => Task.CompletedTask));
        Assert.Equal(2, tracker.Count);

        release.SetResult();
        await tracker.DrainAsync();

        Assert.Equal(0, tracker.Count);
        Assert.True(tracker.TryStart(() => Task.CompletedTask));
        await tracker.DrainAsync();
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public async Task Tracker_reports_operation_failure_and_still_releases_capacity()
    {
        Exception? reported = null;
        var tracker = new BoundedOperationTracker(1, exception => reported = exception);

        Assert.True(tracker.TryStart(() => Task.FromException(new InvalidOperationException("boom"))));
        await tracker.DrainAsync();

        Assert.IsType<InvalidOperationException>(reported);
        Assert.Equal(0, tracker.Count);
        Assert.True(tracker.TryStart(() => Task.CompletedTask));
        await tracker.DrainAsync();
    }
}
