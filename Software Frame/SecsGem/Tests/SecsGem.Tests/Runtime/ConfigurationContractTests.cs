// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ConfigurationContractTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Tests、SecsGem.Runtime.Variable、SecsGem.Core、SecsGem.Tests.Fakes、SecsGem.Tests.Infrastructure、Xunit。导入不代表所有类型都会在运行时加载。
using SecsGem.Tests;
using SecsGem.Runtime.Variable;
using SecsGem.Core;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Runtime;

/// <summary>
/// ConfigurationContractTests 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ConfigurationContractTests
{
    /// <summary>
    /// 测试相关入口 Report_And_Event_Definitions_RoundTrip_Through_Core_Contract；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、GetTempPath、ToString、NewGuid、Create；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Report_And_Event_Definitions_RoundTrip_Through_Core_Contract()
    {
        var root = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = RuntimeTestFixture.Create(new InMemorySecsTransport());
            var catalog = runtime.VariableManager;
            catalog.ReportID.FilePath = Path.Combine(root, "reports");
            catalog.CEID.FilePath = Path.Combine(root, "events");
            catalog.ReportID.Datas.Add(new ReportInfo
            {
                ID = "7", Name = "LotReport", VID_Collection = "1,2"
            });
            catalog.CEID.Datas.Add(new CEIDInfo
            {
                ID = "9", Name = "LotChanged", ReportID_Collection = "7", Enable = true
            });

            ISecsGemConfiguration configuration = runtime;
            configuration.SaveReports();
            configuration.SaveEvents();
            catalog.ReportID.Datas.Clear();
            catalog.CEID.Datas.Clear();

            Assert.Contains(configuration.LoadReports(), x =>
                x.Id == "7" && x.Name == "LotReport" && x.VariableIds == "1,2");
            Assert.Contains(configuration.LoadEvents(), x =>
                x.Id == "9" && x.Name == "LotChanged" && x.ReportIds == "7" && x.Enabled);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // ============================================================ M02：重载真正把页面上的编辑落盘

    /// <summary>
    /// 页面上改过的报表必须能存下去并读回来。
    /// 改造前这一条不可能成立：<c>LoadReports()</c> 返回的是映射出来的快照，
    /// <c>SaveReports()</c>（无参）序列化的是运行时自己那张表 —— 两者之间没有任何连接，
    /// 页面改的 Name / VariableIds 根本到不了 <c>ReportID.csv</c>。
    /// </summary>
    [Fact]
    public void Edited_Reports_Persist_Through_The_Overload()
    {
        var root = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = RuntimeTestFixture.Create(new InMemorySecsTransport());
            var catalog = runtime.VariableManager;
            catalog.ReportID.FilePath = Path.Combine(root, "reports");
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "7", Name = "before", VID_Collection = "1,2" });

            ISecsGemConfiguration configuration = runtime;

            var edited = configuration.LoadReports()
                .Select(x => x with { Name = "after", VariableIds = "1,2,3" })
                .ToArray();
            configuration.SaveReports(edited);

            catalog.ReportID.Datas.Clear();          // 丢掉内存，逼它从磁盘读
            // 按 id 取，不能用 Single：CSV 缺失时 ReportID.Load() 会先载入四条打底报表
            var report = configuration.LoadReports().Single(x => x.Id == "7");
            Assert.Equal("7", report.Id);
            Assert.Equal("after", report.Name);
            Assert.Equal("1,2,3", report.VariableIds);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>事件表同理，连 <c>Enable</c> 这种 bool 也要跟着存。</summary>
    [Fact]
    public void Edited_Events_Persist_Through_The_Overload()
    {
        var root = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = RuntimeTestFixture.Create(new InMemorySecsTransport());
            var catalog = runtime.VariableManager;
            catalog.CEID.FilePath = Path.Combine(root, "events");
            catalog.CEID.Datas.Add(new CEIDInfo { ID = "9", Name = "before", ReportID_Collection = "7", Enable = true });

            ISecsGemConfiguration configuration = runtime;

            var edited = configuration.LoadEvents()
                .Select(x => x with { Name = "after", ReportIds = "7,8", Enabled = false })
                .ToArray();
            configuration.SaveEvents(edited);

            catalog.CEID.Datas.Clear();
            // 同上：缺 CSV 时 CEID.Load() 会先载入五条控制状态打底事件
            var item = configuration.LoadEvents().Single(x => x.Id == "9");
            Assert.Equal("9", item.Id);
            Assert.Equal("after", item.Name);
            Assert.Equal("7,8", item.ReportIds);
            Assert.False(item.Enabled);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 页面上删掉的行必须真的从磁盘消失。合并逻辑若是"只增不删"，这一条会红。
    /// </summary>
    [Fact]
    public void Rows_Removed_On_The_Page_Disappear_From_The_File()
    {
        var root = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = RuntimeTestFixture.Create(new InMemorySecsTransport());
            var catalog = runtime.VariableManager;
            catalog.ReportID.FilePath = Path.Combine(root, "reports");
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "7", Name = "keep", VID_Collection = "1" });
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "8", Name = "drop", VID_Collection = "2" });

            ISecsGemConfiguration configuration = runtime;
            var kept = configuration.LoadReports().Where(x => x.Id == "7").ToArray();
            configuration.SaveReports(kept);

            catalog.ReportID.Datas.Clear();
            var remaining = configuration.LoadReports();
            Assert.Contains(remaining, x => x.Id == "7");
            Assert.DoesNotContain(remaining, x => x.Id == "8");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 空集合的报表/事件不能炸掉新增/删除时的全表映射。
    /// <para>
    /// 回归现场：删 Report 报 "Value cannot be null. (Parameter 'values')"。
    /// <c>ReportInfo.VIDID</c> 从未初始化，CSV 里空格子还会被 <c>ReadCsv</c> 跳过，
    /// 于是 <c>VID_Collection</c> 的 getter 里 <c>string.Join(",", null)</c> 直接抛；
    /// 而新增/删除都要把<b>全表</b>映射成定义，哪一行空就炸。事件同理。
    /// </para>
    /// </summary>
    [Fact]
    public void Empty_Collections_Survive_The_Full_Table_Mapping()
    {
        var root = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = RuntimeTestFixture.Create(new InMemorySecsTransport());
            var catalog = runtime.VariableManager;
            catalog.ReportID.FilePath = Path.Combine(root, "reports");
            catalog.CEID.FilePath = Path.Combine(root, "events");
            // 一行空集合（没走 setter，跟 CSV 空格子读进来的状态一样），一行正常
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "7", Name = "empty" });
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "8", Name = "normal", VID_Collection = "1" });
            catalog.CEID.Datas.Add(new CEIDInfo { ID = "9", Name = "empty" });

            ISecsGemConfiguration configuration = runtime;

            // 这正是 DeleteReport/DeleteEvent 做的事：全表映射后落盘
            configuration.SaveReports(configuration.LoadReports().Where(x => x.Id == "8").ToArray());
            configuration.SaveEvents(Array.Empty<SecsGem.Core.SecsGemEventDefinition>());

            catalog.ReportID.Datas.Clear();
            var remaining = configuration.LoadReports();
            Assert.Contains(remaining, x => x.Id == "8");
            Assert.DoesNotContain(remaining, x => x.Id == "7");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 空串与多余逗号都不能变成"空 ID"。
    /// <c>ReportInfo.VID_Collection</c> 的 setter 直接 <c>Split(',')</c>，空串会留下一个空 ID，
    /// 之后 <c>ReportID.Update()</c> 会抛 "VID not exist in ReportID" —— 只在运行期炸的坑。
    /// <para>
    /// 两个输入都要覆盖：空串走 <c>IsNullOrWhiteSpace</c> 提前返回，而 <c>"1,,2"</c> 只有
    /// <c>SplitList</c> 里的 <c>Where(x =&gt; x.Length &gt; 0)</c> 兜得住 —— 只测前者的话，
    /// 把那个过滤器删掉测试照样是绿的（已实测）。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("1,,2", 2)]
    public void Blank_Entries_In_VariableIds_Do_Not_Become_Empty_VIDs(string variableIds, int expectedCount)
    {
        var root = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = RuntimeTestFixture.Create(new InMemorySecsTransport());
            var catalog = runtime.VariableManager;
            catalog.ReportID.FilePath = Path.Combine(root, "reports");
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "7", Name = "noVid", VID_Collection = "1,2" });

            ISecsGemConfiguration configuration = runtime;
            configuration.SaveReports(
                configuration.LoadReports().Select(x => x with { VariableIds = variableIds }).ToArray());

            var ids = catalog.ReportID.GetDatas().Single(x => x.ID == "7").VIDID;
            Assert.Equal(expectedCount, ids.Count);
            Assert.DoesNotContain("", ids);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 测试相关入口 Core_Alarm_Report_Updates_Local_Alarm_State_And_Sends_S5F1；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、Add、True、ReportAlarmAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Core_Alarm_Report_Updates_Local_Alarm_State_And_Sends_S5F1()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        runtime.VariableManager.Alarm.Datas.Add(new AlarmInfo
        {
            ID = "11", Description = "Pressure", Code = 0
        });

        Assert.True(await runtime.ReportAlarmAsync(new SecsGemAlarm("11", "Pressure", 1)));
        Assert.Equal((byte)1, runtime.VariableManager.Alarm.GetDatas().Single().Code);
        Assert.Equal("S5F1", transport.LastSentOrder);
    }
}