using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

public sealed class DataPointValueUpdateTests
{
    [Fact]
    public void Successful_sample_is_published_as_one_quality_update()
    {
        IDataPoint point = new DataPoint("Temperature", DataPointType.Int16, "40001");
        var timestamp = new DateTimeOffset(2026, 9, 4, 8, 30, 0, TimeSpan.Zero);
        var sourceTimestamp = timestamp.AddMilliseconds(-20);

        point.ApplyRead(new byte[] { 0x12, 0x34 }, timestamp, sourceTimestamp);

        var update = point.Current;
        Assert.Equal((short)0x1234, update.Value);
        Assert.Equal(DataQuality.Good, update.Quality);
        Assert.Equal(timestamp, update.Timestamp);
        Assert.Equal(sourceTimestamp, update.SourceTimestamp);
        Assert.Null(update.Error);
    }

    [Fact]
    public void Failed_sample_preserves_last_value_and_records_the_error()
    {
        IDataPoint point = new DataPoint("Temperature", DataPointType.Int16, "40001");
        var goodTimestamp = new DateTimeOffset(2026, 9, 4, 8, 30, 0, TimeSpan.Zero);
        var failedTimestamp = goodTimestamp.AddSeconds(1);
        var error = new TimeoutException("读取超时");
        point.ApplyRead(new byte[] { 0x12, 0x34 }, goodTimestamp);

        point.ApplyFailure(error, failedTimestamp);

        var update = point.Current;
        Assert.Equal((short)0x1234, update.Value);
        Assert.Equal(DataQuality.Bad, update.Quality);
        Assert.Equal(failedTimestamp, update.Timestamp);
        Assert.Same(error, update.Error);
    }

    [Fact]
    public void Decode_failure_does_not_replace_the_last_good_sample()
    {
        IDataPoint point = new DataPoint("Temperature", DataPointType.Int16, "40001");
        var timestamp = new DateTimeOffset(2026, 9, 4, 8, 30, 0, TimeSpan.Zero);
        point.ApplyRead(new byte[] { 0x12, 0x34 }, timestamp);

        Assert.ThrowsAny<Exception>(() =>
            point.ApplyRead(new byte[] { 0x12 }, timestamp.AddSeconds(1)));

        Assert.Equal((short)0x1234, point.Current.Value);
        Assert.Equal(timestamp, point.Current.Timestamp);
        Assert.Equal(new byte[] { 0x12, 0x34 }, point.RawValue);
    }

    [Fact]
    public void Unsupported_generic_value_type_is_rejected_instead_of_falling_back_to_int16()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new DataPoint<decimal>("Price", "HR1"));

        Assert.Contains(typeof(decimal).FullName!, exception.Message);
    }

    [Fact]
    public void Failed_typed_conversion_is_reported_instead_of_returning_default()
    {
        var point = new DataPoint<int>("Counter", "HR1");
        point.ApplyRead(
            new byte[] { 0, 0, 0, 42 },
            new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));

        var exception = Assert.Throws<InvalidCastException>(() => point.As<Guid>());

        Assert.Contains(typeof(int).FullName!, exception.Message);
        Assert.Contains(typeof(Guid).FullName!, exception.Message);
        Assert.NotNull(exception.InnerException);
    }
}
