using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using Xunit;

namespace CommunicationNew.Tests.Core;

public sealed class DataPointTests
{
    [Fact]
    public void Negative_length_assignment_is_rejected()
    {
        IDataPoint point = new DataPoint("Counter", DataPointType.Int32, "HR1");

        Assert.Throws<ArgumentOutOfRangeException>(() => point.Length = -1);
    }

    [Fact]
    public void Negative_constructor_length_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataPoint("Counter", DataPointType.Int32, "HR1", -1));
    }

    [Fact]
    public void Negative_scan_interval_assignment_is_rejected()
    {
        IDataPoint point = new DataPoint("Counter", DataPointType.Int32, "HR1");

        Assert.Throws<ArgumentOutOfRangeException>(() => point.ScanIntervalMs = -1);
    }
}
