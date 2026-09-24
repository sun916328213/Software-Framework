using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using CommunicationNew.Core.Services;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

public sealed class DataPointScanPlannerTests
{
    [Fact]
    public void Only_points_whose_scan_interval_has_elapsed_are_due()
    {
        var sampledAt = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        IDataPoint fast = new DataPoint("Fast", DataPointType.Int16, "HR1")
        {
            ScanIntervalMs = 100,
        };
        IDataPoint slow = new DataPoint("Slow", DataPointType.Int16, "HR2")
        {
            ScanIntervalMs = 1000,
        };
        fast.ApplyRead(new byte[] { 0, 1 }, sampledAt);
        slow.ApplyRead(new byte[] { 0, 2 }, sampledAt);

        var plan = DataPointScanPlanner.Plan(
            new[] { slow, fast },
            sampledAt.AddMilliseconds(100),
            TimeSpan.FromSeconds(5));

        Assert.Collection(plan.DuePoints, point => Assert.Same(fast, point));
        Assert.Equal(TimeSpan.Zero, plan.DelayUntilNextScan);
    }

    [Fact]
    public void Plan_waits_until_the_nearest_point_deadline()
    {
        var sampledAt = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        IDataPoint fast = new DataPoint("Fast", DataPointType.Int16, "HR1")
        {
            ScanIntervalMs = 100,
        };
        IDataPoint slow = new DataPoint("Slow", DataPointType.Int16, "HR2")
        {
            ScanIntervalMs = 1000,
        };
        fast.ApplyRead(new byte[] { 0, 1 }, sampledAt);
        slow.ApplyRead(new byte[] { 0, 2 }, sampledAt);

        var plan = DataPointScanPlanner.Plan(
            new[] { fast, slow },
            sampledAt.AddMilliseconds(40),
            TimeSpan.FromSeconds(5));

        Assert.Empty(plan.DuePoints);
        Assert.Equal(TimeSpan.FromMilliseconds(60), plan.DelayUntilNextScan);
    }

    [Fact]
    public void New_readable_points_are_due_and_zero_interval_inherits_the_default()
    {
        var now = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        IDataPoint inherited = new DataPointDefinition
        {
            Name = "Inherited",
            Address = "HR1",
            ScanIntervalMs = 0,
        }.ToDataPoint();
        IDataPoint disabled = new DataPoint("Disabled", DataPointType.Int16, "HR2")
        {
            Readable = false,
        };

        var firstPlan = DataPointScanPlanner.Plan(
            new[] { inherited, disabled },
            now,
            TimeSpan.FromMilliseconds(250));
        Assert.Collection(firstPlan.DuePoints, point => Assert.Same(inherited, point));

        inherited.ApplyRead(new byte[] { 0, 1 }, now);
        var nextPlan = DataPointScanPlanner.Plan(
            new[] { inherited, disabled },
            now.AddMilliseconds(100),
            TimeSpan.FromMilliseconds(250));
        Assert.Empty(nextPlan.DuePoints);
        Assert.Equal(TimeSpan.FromMilliseconds(150), nextPlan.DelayUntilNextScan);
    }
}
