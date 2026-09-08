using CommunicationNew.Abstractions;
using CommunicationNew.Core.Converters;
using CommunicationNew.Core.Interfaces;
using Xunit;

namespace CommunicationNew.Tests.Converters;

public sealed class ValueConverterFactoryTests
{
    [Fact]
    public async Task All_data_formats_remain_isolated_during_concurrent_conversion()
    {
        var cases = new[]
        {
            (Format: DataFormat.ABCD, Bytes: new byte[] { 1, 2, 3, 4 }),
            (Format: DataFormat.BADC, Bytes: new byte[] { 2, 1, 4, 3 }),
            (Format: DataFormat.CDAB, Bytes: new byte[] { 3, 4, 1, 2 }),
            (Format: DataFormat.DCBA, Bytes: new byte[] { 4, 3, 2, 1 }),
        };

        // 每种格式独立占用一个并发任务，反复经过共享 factory cache 验证无跨设备污染。
        var conversions = cases.Select(testCase => Task.Run(() =>
        {
            for (var iteration = 0; iteration < 10_000; iteration++)
            {
                var converter = ValueConverterFactory.Create(
                    DataPointType.Int32,
                    testCase.Format);
                Assert.Equal(0x01020304, converter.Decode(testCase.Bytes));
            }
        }));

        await Task.WhenAll(conversions);
    }

    [Fact]
    public void Device_specific_data_formats_do_not_share_mutable_endianness()
    {
        var abcd = ValueConverterFactory.Create(DataPointType.Int32, DataFormat.ABCD);
        var dcba = ValueConverterFactory.Create(DataPointType.Int32, DataFormat.DCBA);

        Assert.Equal(0x01020304, abcd.Decode(new byte[] { 1, 2, 3, 4 }));
        Assert.Equal(0x01020304, dcba.Decode(new byte[] { 4, 3, 2, 1 }));
        Assert.Equal(0x01020304, abcd.Decode(new byte[] { 1, 2, 3, 4 }));
    }
}
