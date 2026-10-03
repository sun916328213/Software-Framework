// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DataPointValueUpdateTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Xunit;

namespace Communication.Tests.Runtime;

/// <summary>
/// DataPointValueUpdateTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class DataPointValueUpdateTests
{
    /// <summary>
    /// 验证 Successful_sample_is_published_as_one_quality_update 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AddMilliseconds、ApplyRead、Equal、Null；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Failed_sample_preserves_last_value_and_records_the_error 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AddSeconds、ApplyRead、ApplyFailure、Equal、Same；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Decode_failure_does_not_replace_the_last_good_sample 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ApplyRead、ThrowsAny、AddSeconds、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Unsupported_generic_value_type_is_rejected_instead_of_falling_back_to_int16 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Unsupported_generic_value_type_is_rejected_instead_of_falling_back_to_int16()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new DataPoint<decimal>("Price", "HR1"));

        Assert.Contains(typeof(decimal).FullName!, exception.Message);
    }

    /// <summary>
    /// 验证 Failed_typed_conversion_is_reported_instead_of_returning_default 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ApplyRead、Throws、As、Contains、NotNull；具体条件和先后次序见方法体。
    /// </summary>
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
