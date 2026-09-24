using CommunicationNew.Abstractions;
using CommunicationNew.Core.Converters;
using Xunit;

namespace CommunicationNew.Tests.Converters;

public sealed class DataFormatCodecTests
{
    [Fact]
    public void Legacy_runtime_codec_remains_compatible_during_namespace_migration()
    {
#pragma warning disable CS0618 // 本测试专门锁定旧命名空间的迁移兼容性。
        var codec = new CommunicationNew.Runtime.Conversion.DataFormatCodec(DataFormat.DCBA);
#pragma warning restore CS0618

        Assert.Equal(0x01020304, codec.DecodeInt32(new byte[] { 4, 3, 2, 1 }));
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, codec.EncodeInt32(0x01020304));
    }

    [Theory]
    [InlineData(DataFormat.ABCD, 1, 2, 3, 4)]
    [InlineData(DataFormat.BADC, 2, 1, 4, 3)]
    [InlineData(DataFormat.CDAB, 3, 4, 1, 2)]
    [InlineData(DataFormat.DCBA, 4, 3, 2, 1)]
    public void Int32_round_trips_for_each_device_data_format(
        DataFormat format,
        byte first,
        byte second,
        byte third,
        byte fourth)
    {
        var codec = new DataFormatCodec(format);
        var deviceBytes = new[] { first, second, third, fourth };

        Assert.Equal(0x01020304, codec.DecodeInt32(deviceBytes));
        Assert.Equal(deviceBytes, codec.EncodeInt32(0x01020304));
    }
}
