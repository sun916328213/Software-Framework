using CommunicationNew.Abstractions;
using CommunicationNew.Core.Converters;
using CommunicationNew.Core.Interfaces;
using Xunit;

namespace CommunicationNew.Tests.Converters;

public sealed class ValueConverterFactoryTests
{
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
