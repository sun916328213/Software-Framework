// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusReadBatchPlannerTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models、Communication.Runtime.Protocols.Modbus、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Communication.Runtime.Protocols.Modbus;
using Xunit;

namespace Communication.Tests.Protocols.Modbus;

/// <summary>
/// ModbusReadBatchPlannerTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusReadBatchPlannerTests
{
    /// <summary>
    /// 验证 One_hundred_adjacent_registers_are_merged_into_one_request 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、Select、Range、Single、Plan；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 A_batch_response_updates_every_point_from_its_own_slice 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Single、Plan、ApplyResponse、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Protocol_limit_splits_one_hundred_and_twenty_six_adjacent_registers 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、Select、Range、Plan、Collection；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 A_point_wider_than_the_configured_request_limit_is_rejected 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws、Plan、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void A_point_wider_than_the_configured_request_limit_is_rejected()
    {
        IDataPoint point = new DataPoint("Counter", DataPointType.Int64, "HR1");

        var exception = Assert.Throws<ArgumentException>(() =>
            ModbusReadBatchPlanner.Plan(new[] { point }, maximumRegistersPerRequest: 2));

        Assert.Contains("Counter", exception.Message);
    }

    /// <summary>
    /// 验证 Address_gaps_and_register_regions_are_not_merged 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Plan、Equal、Contains、Select、Where；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Invalid_response_length_is_rejected_before_any_point_is_updated 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Single、Plan、Throws、ApplyResponse、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Eight_adjacent_coils_are_merged_and_unpacked_low_bit_first 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、Select、Range、Single、Plan；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Two_thousand_and_one_adjacent_coils_are_split_at_the_protocol_limit 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、Select、Range、Plan、Collection；具体条件和先后次序见方法体。
    /// </summary>
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
