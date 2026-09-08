using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using CommunicationNew.Protocols.Modbus;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Modbus;

public sealed class ModbusReadBatchPlannerTests
{
    [Fact]
    public void One_hundred_adjacent_registers_are_merged_into_one_request()
    {
        var points = Enumerable.Range(1, 100)
            .Select(number => (IDataPoint)new DataPoint(
                $"Register{number}",
                DataPointType.Int16,
                $"HR{number}"))
            .ToArray();

        var batch = Assert.Single(ModbusReadBatchPlanner.Plan(points));

        Assert.Equal(ModbusRegion.HoldingRegister, batch.Region);
        Assert.Equal((ushort)0, batch.StartAddress);
        Assert.Equal((ushort)100, batch.RegisterCount);
        Assert.Equal(100, batch.Slices.Count);
    }

    [Fact]
    public void A_batch_response_updates_every_point_from_its_own_slice()
    {
        IDataPoint first = new DataPoint("First", DataPointType.Int16, "HR1");
        IDataPoint second = new DataPoint("Second", DataPointType.UInt16, "HR2");
        var batch = Assert.Single(ModbusReadBatchPlanner.Plan(new[] { second, first }));
        var timestamp = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

        batch.ApplyResponse(new byte[] { 0x12, 0x34, 0xAB, 0xCD }, timestamp);

        Assert.Equal((short)0x1234, first.Current.Value);
        Assert.Equal((ushort)0xABCD, second.Current.Value);
        Assert.Equal(timestamp, first.Current.Timestamp);
        Assert.Equal(timestamp, second.Current.Timestamp);
    }

    [Fact]
    public void Protocol_limit_splits_one_hundred_and_twenty_six_adjacent_registers()
    {
        var points = Enumerable.Range(1, 126)
            .Select(number => (IDataPoint)new DataPoint(
                $"Register{number}",
                DataPointType.Int16,
                $"HR{number}"))
            .ToArray();

        var batches = ModbusReadBatchPlanner.Plan(points);

        Assert.Collection(
            batches,
            first => Assert.Equal((ushort)125, first.RegisterCount),
            second => Assert.Equal((ushort)1, second.RegisterCount));
    }

    [Fact]
    public void A_point_wider_than_the_configured_request_limit_is_rejected()
    {
        IDataPoint point = new DataPoint("Counter", DataPointType.Int64, "HR1");

        var exception = Assert.Throws<ArgumentException>(() =>
            ModbusReadBatchPlanner.Plan(new[] { point }, maximumRegistersPerRequest: 2));

        Assert.Contains("Counter", exception.Message);
    }

    [Fact]
    public void Address_gaps_and_register_regions_are_not_merged()
    {
        IDataPoint holding1 = new DataPoint("Holding1", DataPointType.Int16, "HR1");
        IDataPoint holding3 = new DataPoint("Holding3", DataPointType.Int16, "HR3");
        IDataPoint input1 = new DataPoint("Input1", DataPointType.Int16, "IR1");

        var batches = ModbusReadBatchPlanner.Plan(new[] { holding3, input1, holding1 });

        Assert.Equal(3, batches.Count);
        Assert.Contains(batches, batch =>
            batch.Region == ModbusRegion.InputRegister && batch.StartAddress == 0);
        Assert.Equal(
            new ushort[] { 0, 2 },
            batches
                .Where(batch => batch.Region == ModbusRegion.HoldingRegister)
                .Select(batch => batch.StartAddress));
    }

    [Fact]
    public void Invalid_response_length_is_rejected_before_any_point_is_updated()
    {
        IDataPoint first = new DataPoint("First", DataPointType.Int16, "HR1");
        IDataPoint second = new DataPoint("Second", DataPointType.Int16, "HR2");
        var batch = Assert.Single(ModbusReadBatchPlanner.Plan(new[] { first, second }));

        Assert.Throws<InvalidDataException>(() =>
            batch.ApplyResponse(
                new byte[] { 0x12, 0x34 },
                new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero)));
        Assert.Equal(DataQuality.Uncertain, first.Current.Quality);
        Assert.Equal(DataQuality.Uncertain, second.Current.Quality);
    }

    [Fact]
    public void Eight_adjacent_coils_are_merged_and_unpacked_low_bit_first()
    {
        var points = Enumerable.Range(1, 8)
            .Select(number => (IDataPoint)new DataPoint(
                $"Coil{number}",
                DataPointType.Bool,
                $"C{number}"))
            .ToArray();
        var batch = Assert.Single(ModbusReadBatchPlanner.Plan(points));

        batch.ApplyResponse(
            new byte[] { 0b1000_0101 },
            new DateTimeOffset(2026, 9, 4, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal(ModbusRegion.Coil, batch.Region);
        Assert.Equal((ushort)8, batch.RegisterCount);
        Assert.Equal(
            new[] { true, false, true, false, false, false, false, true },
            points.Select(point => Assert.IsType<bool>(point.Current.Value)));
    }

    [Fact]
    public void Two_thousand_and_one_adjacent_coils_are_split_at_the_protocol_limit()
    {
        var points = Enumerable.Range(1, 2_001)
            .Select(number => (IDataPoint)new DataPoint(
                $"Coil{number}",
                DataPointType.Bool,
                $"C{number}"))
            .ToArray();

        var batches = ModbusReadBatchPlanner.Plan(points);

        Assert.Collection(
            batches,
            first => Assert.Equal((ushort)2_000, first.Quantity),
            second => Assert.Equal((ushort)1, second.Quantity));
    }
}
