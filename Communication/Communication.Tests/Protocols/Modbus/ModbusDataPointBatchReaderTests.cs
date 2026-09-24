using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using CommunicationNew.Protocols.Modbus;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Modbus;

public sealed class ModbusDataPointBatchReaderTests
{
    [Fact]
    public async Task Bit_batch_reader_sends_bit_quantity_and_returns_one_byte_per_point()
    {
        var points = Enumerable.Range(1, 8)
            .Select(number => (IDataPoint)new DataPoint(
                $"Input{number}",
                DataPointType.Bool,
                $"DI{number}"))
            .ToArray();
        var calls = new List<(string Address, int Quantity)>();

        var results = await ModbusDataPointBatchReader.ReadPointsAsync(
            points,
            (address, quantity, _) =>
            {
                calls.Add((address, quantity));
                return Task.FromResult(new byte[] { 0b0101_0011 });
            },
            CancellationToken.None);

        Assert.Equal(("DI1", 8), Assert.Single(calls));
        Assert.Equal(
            new byte[] { 1, 1, 0, 0, 1, 0, 1, 0 },
            results.Select(result => Assert.Single(result.RawValue!)));
    }
}
