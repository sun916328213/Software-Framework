// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：QueryPolicyTests、CapacityReader、TableReader、Row、LogReader、StringReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.Queries.Data、WPFControls.Queries.Logs、Xunit。导入不代表所有类型都会在运行时加载。
using WPFControls.Queries.Data;
using WPFControls.Queries.Logs;
using Xunit;

namespace WPFControls.Tests.Queries;

/// <summary>
/// QueryPolicyTests 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class QueryPolicyTests
{
    /// <summary>
    /// 验证 QueryClassesDependOnReaderContractsInsideTheUnifiedUiLibrary 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、GetName、Select、SelectMany、GetConstructors；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void QueryClassesDependOnReaderContractsInsideTheUnifiedUiLibrary()
    {
        Assert.Equal("WPFControls", typeof(TableQueryService<>).Assembly.GetName().Name);
        foreach (var type in new[] { typeof(TableQueryService<>), typeof(CapacityStatisticsQueryService<>), typeof(LogHistoryQueryService<>) })
        {
            var dependencies = type.GetConstructors().SelectMany(ctor => ctor.GetParameters()).Select(parameter => parameter.ParameterType);
            Assert.All(dependencies, dependency => Assert.StartsWith("WPFControls.Queries.", dependency.Namespace));
        }
        }

    /// <summary>
    /// 验证 TableQueryKeepsDateNormalizationInclusiveNextDayAndExactId 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AddDays、Query、Equal、Throws、Add；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void TableQueryKeepsDateNormalizationInclusiveNextDayAndExactId()
    {
        var reader = new TableReader();
        var service = new TableQueryService<string>(reader);
        var start = new DateTime(2026, 10, 1, 13, 14, 15);
        var end = start.AddDays(2);
        var rows = service.Query(start, end, " id ");
        Assert.Equal((start.Date, end.AddDays(1).Date, " id "), reader.Request);
        Assert.Equal(new[] { "first", "second" }, rows);
        Assert.Equal(1, reader.Enumerations);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)rows).Add("third"));
    }

    /// <summary>
    /// 验证 TableQueryNullSourceReturnsEmptyWithoutChangingTheId 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Empty、Query、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void TableQueryNullSourceReturnsEmptyWithoutChangingTheId()
    {
        var reader = new TableReader { Empty = true };
        Assert.Empty(new TableQueryService<string>(reader).Query(DateTime.Today, DateTime.Today, null));
        Assert.Null(reader.Request.Id);
    }

    /// <summary>
    /// 验证 CapacityKeepsHourlyMergeYieldActiveHoursAndExtraDayDenominator 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Entry、AddDays、Query、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CapacityKeepsHourlyMergeYieldActiveHoursAndExtraDayDenominator()
    {
        var day = new DateTime(2026, 10, 1);
        var reader = new CapacityReader(Entry("one", day, 0, 3, 1), Entry("two", day.AddDays(1), 0, 2, 0));
        var result = new CapacityStatisticsQueryService<string>(reader).Query(day, day);
        Assert.Equal((day, day.AddDays(1)), reader.Request);
        Assert.Equal(new[] { "one", "two" }, result.Rows);
        Assert.Equal((5, 1, 6, 83.33, 2, 4.17),
            (result.GoodCount, result.DefectCount, result.TotalOutput, result.YieldRate, result.RunTime, result.UtilizationRate));
        Assert.Equal(24, result.Trend.Count);
        Assert.Equal(new CapacityTrendPoint("00:00", 5, 1, 6), result.Trend[0]);
        Assert.Equal(new CapacityTrendPoint("23:00", 0, 0, 0), result.Trend[23]);
    }

    /// <summary>
    /// 验证 CapacityKeepsInclusiveDailyTrendAndMissingDays 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Entry、AddDays、Query、Equal、Select；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CapacityKeepsInclusiveDailyTrendAndMissingDays()
    {
        var day = new DateTime(2026, 10, 1);
        var reader = new CapacityReader(Entry("end", day.AddDays(3), 23, 7, 2));
        var result = new CapacityStatisticsQueryService<string>(reader).Query(day, day.AddDays(2));
        Assert.Equal(new[] { "10-01", "10-02", "10-03", "10-04" }, result.Trend.Select(point => point.Label));
        Assert.Equal(new[] { 0, 0, 0, 9 }, result.Trend.Select(point => point.Total));
        Assert.Equal(1.04, result.UtilizationRate);
    }

    /// <summary>
    /// 验证 CapacityKeepsTwoDayThresholdAndNonNormalizedStart 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Query、AddDays；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CapacityKeepsTwoDayThresholdAndNonNormalizedStart()
    {
        var day = new DateTime(2026, 10, 1, 6, 0, 0);
        var reader = new CapacityReader();
        var service = new CapacityStatisticsQueryService<string>(reader);
        Assert.Equal(24, service.Query(day, day.AddDays(1)).Trend.Count);
        var daily = service.Query(day, day.AddDays(2));
        Assert.Equal(day, reader.Request.Start);
        Assert.Equal(4, daily.Trend.Count);
        Assert.Equal(0, daily.YieldRate);
        Assert.Equal(0, daily.UtilizationRate);
    }

    /// <summary>
    /// 验证 CapacityDoesNotSwallowPersistenceExceptions 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Same、Throws、Query；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CapacityDoesNotSwallowPersistenceExceptions()
    {
        var error = new InvalidOperationException("database");
        var reader = new CapacityReader { Error = error };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
            new CapacityStatisticsQueryService<string>(reader).Query(DateTime.Today, DateTime.Today)));
    }

    /// <summary>
    /// 验证 LogsKeepTimeOfDayAndInclusiveNextDayAcrossMonthBoundary 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 QueryAsync、Equal、AddDays、Single、Throws；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LogsKeepTimeOfDayAndInclusiveNextDayAcrossMonthBoundary()
    {
        var reader = new LogReader();
        var service = new LogHistoryQueryService<Row>(reader);
        var start = new DateTime(2026, 9, 30, 13, 14, 15);
        var rows = await service.QueryAsync(start, start);
        Assert.Equal((start, start.AddDays(1)), reader.Request);
        Assert.Single(rows);
        Assert.Throws<NotSupportedException>(() => ((IList<Row>)rows).Clear());
    }

    /// <summary>
    /// 验证 LogsFilterCaseInsensitivePropertyTextAndKeepUnknownOrEmptyFields 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Take、Filter、nameof、Select；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LogsFilterCaseInsensitivePropertyTextAndKeepUnknownOrEmptyFields()
    {
        var service = new LogHistoryQueryService<Row>(new LogReader());
        Row[] rows = [new("Alarm A", 1), new("alarm b", 2), new(null, 1)];
        Assert.Equal(rows.Take(2), service.Filter(rows, nameof(Row.Info), "ALARM"));
        Assert.Equal(rows, service.Filter(rows, "missing", "alarm"));
        Assert.Equal(rows, service.Filter(rows, nameof(Row.Info), ""));
        Assert.Equal(new[] { rows[1] }, service.Filter(rows, nameof(Row.Level), "2"));
        Assert.Equal(new[] { rows[0], rows[2] }, service.Select(rows, row => row.Level == 1));
    }

    /// <summary>
    /// 验证 LogsPagingKeepsMinimumSizeAndDoesNotClampPageOrKeepSharedState 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、Select、Range、ToString、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LogsPagingKeepsMinimumSizeAndDoesNotClampPageOrKeepSharedState()
    {
        var service = new LogHistoryQueryService<Row>(new LogReader());
        var rows = Enumerable.Range(0, 5).Select(index => new Row(index.ToString(), 1)).ToArray();
        Assert.Equal(rows.Skip(2).Take(2), service.Page(rows, 2, 2));
        Assert.Equal(new[] { rows[1] }, service.Page(rows, 2, 0));
        Assert.Equal(new[] { rows[1] }, service.Page(rows, 2, -5));
        Assert.Equal(rows.Take(2), service.Page(rows, 0, 2));
        Assert.Empty(service.Page(rows, 9, 2));
        Assert.Equal(rows.Take(2), service.Page(rows, 1, 2));
    }

    /// <summary>
    /// 验证 LogsSummaryUsesOnlyTheSuppliedPageAndPreservesGroupOrder 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Summarize、Page、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LogsSummaryUsesOnlyTheSuppliedPageAndPreservesGroupOrder()
    {
        var service = new LogHistoryQueryService<Row>(new LogReader());
        Row[] rows = [new("A", 1), new("info", 0), new("B", 1), new("A", 2), new("outside", 1)];
        var result = service.Summarize(service.Page(rows, 1, 4), row => row.Level != 0, row => row.Info!);
        Assert.Equal(new[] { new LogHistorySummary("A", 2), new LogHistorySummary("B", 1) }, result);
    }

    /// <summary>
    /// 验证 SemiLogStringsRemainOrderedAndUnmodified 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、QueryAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task SemiLogStringsRemainOrderedAndUnmodified()
    {
        var rows = new[] { "SEMI,1", "", "SEMI,1", "原始内容" };
        var query = new LogHistoryQueryService<string>(new StringReader(rows));
        Assert.Equal(rows, await query.QueryAsync(DateTime.Today, DateTime.Today));
    }

    /// <summary>
    /// 验证 Entry 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static CapacityStatisticsEntry<string> Entry(string row, DateTime day, int hour, int good, int defect)
    {
        var ok = new int[24]; var ng = new int[24]; ok[hour] = good; ng[hour] = defect;
        return new(row, day, ok, ng);
    }
    /// <summary>
    /// CapacityReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICapacityStatisticsReader&lt;string&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CapacityReader(params CapacityStatisticsEntry<string>[] rows) : ICapacityStatisticsReader<string>
    {
        public (DateTime Start, DateTime End) Request;
        public Exception? Error;
        /// <summary>
        /// 验证 Read 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public IReadOnlyList<CapacityStatisticsEntry<string>> Read(DateTime start, DateTime end)
        { Request = (start, end); if (Error != null) throw Error; return rows; }
    }
    /// <summary>
    /// TableReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ITableQueryReader&lt;string&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class TableReader : ITableQueryReader<string>
    {
        public (DateTime Start, DateTime End, string? Id) Request;
        public int Enumerations;
        public bool Empty;
        /// <summary>
        /// 验证 Read 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Enumerate；具体条件和先后次序见方法体。
        /// </summary>
        public IEnumerable<string>? Read(DateTime start, DateTime end, string? id)
        { Request = (start, end, id); return Empty ? null : Enumerate(); }
        /// <summary>
        /// 验证 Enumerate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        private IEnumerable<string> Enumerate() { Enumerations++; yield return "first"; yield return "second"; }
    }
    /// <summary>
    /// Row 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    public sealed record Row(string? Info, int Level);
    /// <summary>
    /// LogReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILogHistoryReader&lt;Row&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class LogReader : ILogHistoryReader<Row>
    {
        public (DateTime Start, DateTime End) Request;
        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<IEnumerable<Row>> ReadAsync(DateTime start, DateTime end)
        { Request = (start, end); return Task.FromResult<IEnumerable<Row>>([new("row", 1)]); }
    }
    /// <summary>
    /// StringReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILogHistoryReader&lt;string&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class StringReader(IEnumerable<string> rows) : ILogHistoryReader<string>
    {
        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public Task<IEnumerable<string>> ReadAsync(DateTime start, DateTime end) => Task.FromResult(rows);
    }
}
