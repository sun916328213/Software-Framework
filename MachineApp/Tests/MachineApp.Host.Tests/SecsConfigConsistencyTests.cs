// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主。本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SecsConfigConsistencyTests。校验出厂配置 Config\SecsJson 里的引用链是否闭合。
using MachineApp.Business.Order;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace MachineApp.Tests;

/// <summary>
/// 出厂 GEM 配置的自洽性守卫。
/// <para>
/// 这套配置平时靠「名字」串起来：<c>SendS6F11(EventOrder.X)</c> 按名字查 CEID、
/// <c>ReportID.VID_Collection</c> 按 ID 查变量。任何一处对不上都是<b>静默不发</b>，
/// 编译和测试全绿，只有接上 Host 才发现事件从来没出去过。
/// </para>
/// </summary>
public sealed class SecsConfigConsistencyTests
{
    private static string SecsJsonDir()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Build", "Test-Integration.ps1")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Missing repository");
        return Path.Combine(root, "MachineApp", "MachineApp", "Config", "SecsJson");
    }

    /// <summary>直接读三张变量表；分类来自所在目录名，与运行时 <c>VIDBase.Category</c> 同源。</summary>
    private static List<VIDInfo> LoadVids()
    {
        var dir = SecsJsonDir();
        var vids = new List<VIDInfo>();
        foreach (var category in new[] { "SVID", "DVID", "ECID" })
        {
            var folder = Path.Combine(dir, category);
            Assert.True(Directory.Exists(folder), $"缺目录 {category}");
            foreach (var file in Directory.GetFiles(folder, "*.csv"))
                foreach (var v in Extension.IO.Csv.CSVEx.ReadCsv<VIDInfo>(file))
                {
                    v.Category = category;
                    vids.Add(v);
                }
        }
        return vids;
    }

    /// <summary>VID 的 ID 必须跨 SVID/DVID/ECID 全局唯一——索引和报表引用都是裸 ID。</summary>
    [Fact]
    public void Variable_Ids_Are_Unique_Across_All_Tables_And_Names_Within_Each()
    {
        var vids = LoadVids();
        Assert.NotEmpty(vids);

        Assert.Equal(vids.Count, vids.Select(v => v.ID).Distinct().Count());

        foreach (var group in vids.GroupBy(v => v.Category))
        {
            Assert.Equal(group.Count(), group.Select(v => v.Name).Distinct().Count());
            Assert.All(group, v => Assert.Equal(v.Name.Trim(), v.Name));
            Assert.All(group, v => Assert.True(Enum.TryParse<SecsFormat>(v.DataTypeName, true, out _),
                $"{v.QualifiedId} 的 DataTypeName \"{v.DataTypeName}\" 不是合法 SecsFormat"));
        }
    }

    /// <summary>ReportID 里的每个 VID 引用都要能解析——否则 ReportID.Update 抛异常，整条 S6F11 组包失败。</summary>
    [Fact]
    public void Every_Report_Vid_Reference_Resolves()
    {
        var dir = SecsJsonDir();
        var vids = LoadVids();
        var reports = Extension.IO.Csv.CSVEx.ReadCsv<SecsGem.Runtime.Variable.ReportInfo>(
            Path.Combine(dir, "ReportID", "ReportID.csv"));

        Assert.NotEmpty(reports);
        foreach (var report in reports)
            foreach (var reference in report.VID_Collection.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                Assert.Single(vids, v => v.ID == reference);
    }

    /// <summary>
    /// 核心守卫：<c>EventOrder</c> 里的每个常量都必须在 CEID.csv 里存在同名事件。
    /// 名字对不上时 <c>SendS6F11</c> 会静默跳过，产线上表现为"事件没上报"且无任何报错。
    /// </summary>
    [Fact]
    public void Every_EventOrder_Constant_Exists_As_A_Ceid()
    {
        var ceids = Extension.IO.Csv.CSVEx.ReadCsv<SecsGem.Runtime.Variable.CEIDInfo>(
            Path.Combine(SecsJsonDir(), "CEID", "CEID.csv"));
        var names = ceids.Select(x => x.Name).ToHashSet();

        var constants = typeof(EventOrder)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToArray();

        Assert.NotEmpty(constants);
        foreach (var name in constants)
            Assert.Contains(name, names);
    }

    /// <summary>CEID 里的每个 ReportID 引用都要能解析。</summary>
    [Fact]
    public void Every_Ceid_Report_Reference_Resolves()
    {
        var dir = SecsJsonDir();
        var reportIds = Extension.IO.Csv.CSVEx.ReadCsv<SecsGem.Runtime.Variable.ReportInfo>(
            Path.Combine(dir, "ReportID", "ReportID.csv")).Select(x => x.ID).ToHashSet();

        foreach (var ceid in Extension.IO.Csv.CSVEx.ReadCsv<SecsGem.Runtime.Variable.CEIDInfo>(
                     Path.Combine(dir, "CEID", "CEID.csv")))
            foreach (var reference in ceid.ReportID_Collection.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                Assert.Contains(reference, reportIds);
    }

    /// <summary>
    /// 控制状态的事件必须带上状态值：四条模式类事件挂 DeviceMode 报表（Host 才拿得到新模式），
    /// Device Status 挂 DeviceStatus 报表。原来 Device Online/Offline 只挂 Clock+DeviceInfo，
    /// 收到了也不知道现在是哪个模式。
    /// </summary>
    [Fact]
    public void Control_State_Events_Carry_Their_State_Value()
    {
        var reports = Extension.IO.Csv.CSVEx.ReadCsv<SecsGem.Runtime.Variable.ReportInfo>(
            Path.Combine(SecsJsonDir(), "ReportID", "ReportID.csv"));
        var modeReport = reports.Single(r => r.VID_Collection.Contains("102")).ID;
        var statusReport = reports.Single(r => r.VID_Collection.Contains("103")).ID;

        var ceids = Extension.IO.Csv.CSVEx.ReadCsv<SecsGem.Runtime.Variable.CEIDInfo>(
            Path.Combine(SecsJsonDir(), "CEID", "CEID.csv"));

        foreach (var name in new[] { "Device Mode", "Device Online", "Device Offline", "Device Local" })
            Assert.Contains(modeReport, ReportsOf(ceids, name));

        Assert.Contains(statusReport, ReportsOf(ceids, "Device Status"));
    }

    private static string[] ReportsOf(List<SecsGem.Runtime.Variable.CEIDInfo> ceids, string name) =>
        ceids.Single(c => c.Name == name).ReportID_Collection.Split(',').Select(x => x.Trim()).ToArray();
}
