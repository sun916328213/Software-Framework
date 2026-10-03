// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusDataPointBatchReaderTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models、Communication.Runtime.Protocols.Modbus、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Communication.Runtime.Protocols.Modbus;
using Xunit;

namespace Communication.Tests.Protocols.Modbus;

/// <summary>
/// ModbusDataPointBatchReaderTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusDataPointBatchReaderTests
{
    /// <summary>
    /// 验证 Bit_batch_reader_sends_bit_quantity_and_returns_one_byte_per_point 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、Select、Range、ReadPointsAsync、Add；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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
