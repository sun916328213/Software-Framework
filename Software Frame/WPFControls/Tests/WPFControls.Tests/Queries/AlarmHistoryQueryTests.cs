// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AlarmHistoryQueryTests、Reader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Globalization、WPFControls.Queries.Alarms、Xunit。导入不代表所有类型都会在运行时加载。
using System.Globalization;
using WPFControls.Queries.Alarms;
using Xunit;

namespace WPFControls.Tests.Queries;

/// <summary>
/// AlarmHistoryQueryTests 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class AlarmHistoryQueryTests
{
    /// <summary>
    /// 测试相关入口 Row；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    internal static AlarmHistoryRow Row(string info, string duration = "1.50 s") => new(1, info, "start", "end", duration);
    /// <summary>
    /// Reader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IAlarmHistoryReader。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal sealed class Reader(Func<DateTime, IReadOnlyList<AlarmHistoryRow>> read) : IAlarmHistoryReader
    {
        public List<DateTime> Days { get; } = [];
        /// <summary>
        /// 测试相关入口 ReadDayAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add、FromResult、read；具体条件和先后次序见方法体。
        /// </summary>
        public Task<IReadOnlyList<AlarmHistoryRow>> ReadDayAsync(DateTime day, CancellationToken cancellationToken)
        {
            Days.Add(day);
            return Task.FromResult(read(day));
        }
    }

    /// <summary>
    /// 测试相关入口 IncludesBothCalendarDatesRegardlessOfTimeAndAggregatesVisibleRows；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Row、QueryAsync、Equal、Single、Sum；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task IncludesBothCalendarDatesRegardlessOfTimeAndAggregatesVisibleRows()
    {
        var reader = new Reader(day => day.Day == 1 ? [Row("A"), Row("")] : [Row("A", "2.25 s"), Row("B", "3 s")]);
        var result = await new AlarmHistoryQueryService(reader).QueryAsync(new(2026, 9, 1, 23, 0, 0), new(2026, 9, 2, 1, 0, 0));
        Assert.Equal(new[] { new DateTime(2026, 9, 1), new DateTime(2026, 9, 2) }, reader.Days);
        Assert.Equal(3, result.Rows.Count);
        var a = Assert.Single(result.Summaries, x => x.Info == "A");
        Assert.Equal(2, a.Count);
        Assert.Equal(3.75, a.DurationSeconds);
        Assert.Equal(2d / 3, a.Proportion);
        Assert.Equal(1d, result.Summaries.Sum(x => x.Proportion));
        Assert.Equal("1.50 s", result.Rows[0].Duration);
    }

    /// <summary>
    /// 测试相关入口 EmptyHistoryAndMaximumDateAreSafe；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 QueryAsync、Empty、Single；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task EmptyHistoryAndMaximumDateAreSafe()
    {
        var reader = new Reader(_ => []);
        var result = await new AlarmHistoryQueryService(reader).QueryAsync(DateTime.MaxValue, DateTime.MaxValue);
        Assert.Empty(result.Rows);
        Assert.Empty(result.Summaries);
        Assert.Single(reader.Days);
    }

    /// <summary>
    /// 测试相关入口 ReversedRangeAndPrecancelledQueryNeverRead；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、QueryAsync、ThrowsAnyAsync、Empty；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ReversedRangeAndPrecancelledQueryNeverRead()
    {
        var reader = new Reader(_ => []);
        var service = new AlarmHistoryQueryService(reader);
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(new(2026, 9, 2), new(2026, 9, 1)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync(DateTime.Today, DateTime.Today, new(true)));
        Assert.Empty(reader.Days);
    }

    /// <summary>
    /// 测试相关入口 CancellationBetweenDaysStopsReading；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Cancel、ThrowsAnyAsync、QueryAsync、Single；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CancellationBetweenDaysStopsReading()
    {
        using var cancellation = new CancellationTokenSource();
        var reader = new Reader(_ => { cancellation.Cancel(); return []; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AlarmHistoryQueryService(reader)
            .QueryAsync(new(2026, 9, 1), new(2026, 9, 3), cancellation.Token));
        Assert.Single(reader.Days);
    }

    /// <summary>
    /// 测试相关入口 InvalidDurationFailsRatherThanPublishingMisleadingTotals；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、QueryAsync、Row；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Theory]
    [InlineData("oops")]
    [InlineData("NaN")]
    [InlineData("-1 s")]
    public async Task InvalidDurationFailsRatherThanPublishingMisleadingTotals(string duration)
    {
        await Assert.ThrowsAsync<FormatException>(() => new AlarmHistoryQueryService(new Reader(_ => [Row("A", duration)]))
            .QueryAsync(DateTime.Today, DateTime.Today));
    }

    /// <summary>
    /// 测试相关入口 SupportsWriterCultureAndInvariantHistoricalDuration；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetCultureInfo、QueryAsync、Row、Equal、Single；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task SupportsWriterCultureAndInvariantHistoricalDuration()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var result = await new AlarmHistoryQueryService(new Reader(_ => [Row("A", "1,25 s"), Row("A", "2.50 s")]))
                .QueryAsync(DateTime.Today, DateTime.Today);
            Assert.Equal(3.75, Assert.Single(result.Summaries).DurationSeconds);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    /// <summary>
    /// 测试相关入口 AlarmQueryConstructorDependsOnlyOnItsReaderContract；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetParameters、Single、GetConstructors、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AlarmQueryConstructorDependsOnlyOnItsReaderContract()
    {
        var parameters = Assert.Single(typeof(AlarmHistoryQueryService).GetConstructors()).GetParameters();
        Assert.Equal(typeof(IAlarmHistoryReader), Assert.Single(parameters).ParameterType);
    }
}
