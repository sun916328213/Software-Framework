// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ValueConverterFactoryTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Abstractions、Communication.Core.Converters、Communication.Core.Interfaces、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Abstractions;
using Communication.Core.Converters;
using Communication.Core.Interfaces;
using Xunit;

namespace Communication.Tests.Converters;

/// <summary>
/// ValueConverterFactoryTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ValueConverterFactoryTests
{
    /// <summary>
    /// 测试相关入口 All_data_formats_remain_isolated_during_concurrent_conversion；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Select、Run、Create、Equal、Decode；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Device_specific_data_formats_do_not_share_mutable_endianness；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、Equal、Decode；具体条件和先后次序见方法体。
    /// </summary>
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
