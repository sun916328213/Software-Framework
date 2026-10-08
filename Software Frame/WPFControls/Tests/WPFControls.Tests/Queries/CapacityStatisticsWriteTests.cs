// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CapacityStatisticsWriteTests、Row、Store。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.Queries.Data、Xunit。导入不代表所有类型都会在运行时加载。
using WPFControls.Queries.Data;
using Xunit;

namespace WPFControls.Tests.Queries;

/// <summary>
/// CapacityStatisticsWriteTests 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class CapacityStatisticsWriteTests
{
    /// <summary>
    /// 测试相关入口 RecordCreatesDayThenIncrementsOnlyTheRequestedHour；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Record、Equal、All、Take；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RecordCreatesDayThenIncrementsOnlyTheRequestedHour()
    {
        var store = new Store();
        var service = new CapacityStatisticsWriteService<Row>(store);
        var time = new DateTime(2026, 10, 1, 23, 59, 59);
        service.Record(time, true);
        service.Record(time, false);
        service.Record(time, true);
        Assert.Equal("20261001", store.Row!.Id);
        Assert.Equal(time.Date, store.Row.At);
        Assert.Equal(2, store.Row.Good[23]);
        Assert.Equal(1, store.Row.Defect[23]);
        Assert.All(store.Row.Good.Take(23), count => Assert.Equal(0, count));
        Assert.Equal(new[] { "query", "add", "query", "update", "query", "update" }, store.Calls);
    }

    /// <summary>
    /// 测试相关入口 HourlySnapshotKeepsOffsetDayAndUsesTheAdjustedHourFromInput；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 SetHourly、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HourlySnapshotKeepsOffsetDayAndUsesTheAdjustedHourFromInput()
    {
        var store = new Store();
        var service = new CapacityStatisticsWriteService<Row>(store);
        var input = new Row { At = new(2026, 10, 2, 0, 0, 0) };
        input.Good[23] = 8;
        input.Defect[23] = 3;
        input.Good[0] = 100;
        service.SetHourly(input);
        Assert.Equal("20261001", store.Row!.Id);
        Assert.Equal(new DateTime(2026, 10, 1), store.Row.At);
        Assert.Equal(8, store.Row.Good[23]);
        Assert.Equal(0, store.Row.Good[0]);
        store.Row.Good[2] = 71;
        input.Good[23] = 9;
        service.SetHourly(input);
        Assert.Equal(9, store.Row.Good[23]);
        Assert.Equal(71, store.Row.Good[2]);
        Assert.Equal(3, store.Row.Defect[23]);
        Assert.Equal(new[] { "query", "add", "query", "update" }, store.Calls);
    }

    /// <summary>
    /// 测试相关入口 NullSnapshotDoesNotTouchPersistenceAndFailuresReachTheUiErrorBoundary；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 SetHourly、Empty、Same、Throws、Record；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NullSnapshotDoesNotTouchPersistenceAndFailuresReachTheUiErrorBoundary()
    {
        var store = new Store();
        var service = new CapacityStatisticsWriteService<Row>(store);
        service.SetHourly(null);
        Assert.Empty(store.Calls);
        store.Error = new InvalidOperationException("database");
        Assert.Same(store.Error, Assert.Throws<InvalidOperationException>(() => service.Record(DateTime.Today, true)));
    }

    /// <summary>
    /// Row 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    private sealed class Row
    {
        public string Id = "";
        public DateTime At;
        public int[] Good = new int[24];
        public int[] Defect = new int[24];
    }

    /// <summary>
    /// Store 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICapacityStatisticsStore&lt;Row&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Store : ICapacityStatisticsStore<Row>
    {
        public Row? Row;
        public Exception? Error;
        public List<string> Calls = [];
        /// <summary>
        /// 测试相关入口 FindDay；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public Row? FindDay(DateTime day) { Calls.Add("query"); if (Error != null) throw Error; return Row; }
        /// <summary>
        /// 测试相关入口 CreateDay；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Row CreateDay(string id, DateTime day) => new() { Id = id, At = day };
        /// <summary>
        /// 测试相关入口 Time；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public DateTime Time(Row row) => row.At;
        /// <summary>
        /// 测试相关入口 Good；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public int Good(Row row, int hour) => row.Good[hour];
        /// <summary>
        /// 测试相关入口 Defect；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public int Defect(Row row, int hour) => row.Defect[hour];
        /// <summary>
        /// 测试相关入口 SetGood；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SetGood(Row row, int hour, int value) => row.Good[hour] = value;
        /// <summary>
        /// 测试相关入口 SetDefect；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SetDefect(Row row, int hour, int value) => row.Defect[hour] = value;
        /// <summary>
        /// 测试相关入口 UpdateDay；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void UpdateDay(DateTime day, Row row) { Calls.Add("update"); Row = row; }
        /// <summary>
        /// 测试相关入口 Add；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void Add(Row row) { Calls.Add("add"); Row = row; }
    }
}
