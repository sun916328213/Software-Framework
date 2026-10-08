// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DataFormatCodecTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Abstractions、Communication.Core.Converters、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Abstractions;
using Communication.Core.Converters;
using Xunit;

namespace Communication.Tests.Converters;

/// <summary>
/// DataFormatCodecTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class DataFormatCodecTests
{
    /// <summary>
    /// 测试相关入口 Legacy_runtime_codec_remains_compatible_during_namespace_migration；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、DecodeInt32、EncodeInt32；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Legacy_runtime_codec_remains_compatible_during_namespace_migration()
    {
#pragma warning disable CS0618 // 本测试专门锁定旧命名空间的迁移兼容性。
        var codec = new Communication.Core.Converters.DataFormatCodec(DataFormat.DCBA);
#pragma warning restore CS0618

        Assert.Equal(0x01020304, codec.DecodeInt32(new byte[] { 4, 3, 2, 1 }));
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, codec.EncodeInt32(0x01020304));
    }

    /// <summary>
    /// 测试相关入口 Int32_round_trips_for_each_device_data_format；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、DecodeInt32、EncodeInt32；具体条件和先后次序见方法体。
    /// </summary>
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
