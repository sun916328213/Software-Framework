// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：VariableDefaultsTests。成员说明可结合下方具体实现阅读。
using SecsGem.Runtime.Variable;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SecsGem.Tests.Runtime;

/// <summary>
/// 钉住「CSV 缺失时的打底项」。改动这些默认值的理由见各表的 LoadDefaults。
/// </summary>
public sealed class VariableDefaultsTests
{
    private static string MissingDir() =>
        Path.Combine(Path.GetTempPath(), "secs_noconfig_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Svid_Falls_Back_To_Clock_Mode_Status_When_Csv_Is_Missing()
    {
        var svid = new SVID { FilePath = MissingDir() };

        svid.Load();

        Assert.Equal(new[] { "101", "102", "103" }, svid.Datas.Select(v => v.ID));
        Assert.Equal(new[] { "Clock", "DeviceMode", "DeviceStatus" }, svid.Datas.Select(v => v.Name));
        // DataType 必须换算出来，否则 S6F11 组包按 ASCII 去拼 U2
        Assert.Equal(SecsFormat.ASCII, svid.Datas[0].DataType);
        Assert.Equal(SecsFormat.U2, svid.Datas[1].DataType);
    }

    [Fact]
    public void Ecid_Falls_Back_To_The_E5_Mandatory_Equipment_Constants()
    {
        var ecid = new ECID { FilePath = MissingDir() };

        ecid.Load();

        Assert.Equal(new[] { "104", "105" }, ecid.Datas.Select(v => v.ID));
        Assert.All(ecid.Datas, v => Assert.Equal(SecsFormat.ASCII, v.DataType));
    }

    [Fact]
    public void Dvid_Has_No_Defaults_Because_Device_Variables_Are_Machine_Specific()
    {
        var dvid = new DVID { FilePath = MissingDir() };

        dvid.Load();

        Assert.Empty(dvid.Datas);
    }

    [Fact]
    public void Report_And_Ceid_Defaults_Only_Reference_Default_Vids()
    {
        var report = new ReportID { FilePath = MissingDir() };
        var ceid = new CEID { FilePath = MissingDir() };

        report.Load();
        ceid.Load();

        var vidIds = new SVID { FilePath = MissingDir() };
        vidIds.Load();
        var ecidVids = new ECID { FilePath = MissingDir() };
        ecidVids.Load();
        var known = vidIds.Datas.Concat(ecidVids.Datas).Select(v => v.ID).ToHashSet();

        foreach (var r in report.Datas)
            Assert.All(r.VID_Collection.Split(','), id => Assert.Contains(id.Trim(), known));   // 报表引用的变量必须存在，否则 Update 时抛
        foreach (var e in ceid.Datas)
            foreach (var id in e.ReportID_Collection.Split(','))
                Assert.Contains(id, report.Datas.Select(r => r.ID));

        // 控制状态三条必须都挂 DeviceMode 报表，Host 才拿得到新模式值
        foreach (var name in new[] { "Device Mode", "Device Local" })
            Assert.Contains("11", ceid.Datas.First(e => e.Name == name).ReportID_Collection);
    }

    /// <summary>
    /// VID 的 ID 跨表重号必须在 <c>VIDManager.Load</c> 就失败。
    /// <para>
    /// 索引（<c>VIDDic_ID</c>）和报表引用（<c>VID_Collection</c>）都是裸 ID，
    /// 重号若放过去就是后写的静默覆盖先写的 —— 现场只表现为"report 里的值其实是别的变量"，
    /// 极难排查。所以把它变成加载期的硬错误。
    /// </para>
    /// </summary>
    [Fact]
    public void Loading_Two_Tables_That_Share_An_Id_Fails_Loudly()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var root = Path.Combine(Path.GetTempPath(), "secs_dup_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCsv(Path.Combine(root, "SVID"), "SVID.csv", "200,ChamberTemperature,F4,25");
            WriteCsv(Path.Combine(root, "DVID"), "DVID.csv", "200,SubstrateID,ASCII,S1");
            catalog.SVID.FilePath = Path.Combine(root, "SVID");
            catalog.DVID.FilePath = Path.Combine(root, "DVID");
            catalog.ECID.FilePath = Path.Combine(root, "ECID");
            catalog.CEID.FilePath = Path.Combine(root, "CEID");
            catalog.ReportID.FilePath = Path.Combine(root, "ReportID");
            catalog.Alarm.FilePath = Path.Combine(root, "Alarm");

            var ex = Assert.Throws<InvalidOperationException>(() => catalog.Load());
            Assert.Contains("200", ex.Message);
            Assert.Contains("ChamberTemperature", ex.Message);
            Assert.Contains("SubstrateID", ex.Message);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        static void WriteCsv(string dir, string file, string body)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, file), "ID,Name,DataTypeName,Value\n" + body + "\n");
        }
    }

    /// <summary>报表引用写裸 ID 即可；顺手也认 "SVID:102" 这种带前缀的写法，别让手工粘贴直接失效。</summary>
    [Fact]
    public void Report_References_Accept_Bare_Ids_And_Tolerate_A_Category_Prefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "secs_ref_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var report = new ReportID { FilePath = root };
            report.Datas.Add(new ReportInfo { ID = "1", Name = "Mixed", VID_Collection = "102, SVID:103" });
            var vids = new List<VIDInfo>
            {
                new() { ID = "102", Name = "DeviceMode", Category = "SVID" },
                new() { ID = "103", Name = "DeviceStatus", Category = "SVID" }
            };

            report.Update(vids);

            Assert.Equal(new[] { "DeviceMode", "DeviceStatus" }, report.Datas[0].VIDInfo.Select(v => v.Name));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 存盘再读回来必须还是原样。
    /// <para>
    /// <c>Save()</c> 走 CSVEx 的属性反射，<c>VIDInfo.QualifiedId</c> 是只读计算属性——
    /// 一旦被写进 CSV，<c>ReadCsv</c> 的 SetValue 就会抛"Property set method not found"，
    /// 表现为界面上新增变量后刷新就整个配置读不出来。
    /// </para>
    /// </summary>
    [Fact]
    public void A_Saved_Variable_Table_Can_Be_Loaded_Back()
    {
        var root = Path.Combine(Path.GetTempPath(), "secs_rt_" + Guid.NewGuid().ToString("N"));
        try
        {
            var table = new SVID { FilePath = root };
            table.Datas.Add(new VIDInfo { ID = "9001", Name = "RoundTrip", DataTypeName = nameof(SecsFormat.U2), Value = (ushort)7 });
            table.Load();                     // 补上 Category / DataType

            table.Save();

            Assert.DoesNotContain("QualifiedId", File.ReadAllText(Path.Combine(root, "SVID.csv")));
            var reloaded = new SVID { FilePath = root };
            reloaded.Load();
            var back = reloaded.Datas.Single(x => x.ID == "9001");
            Assert.Equal("RoundTrip", back.Name);
            Assert.Equal("SVID", back.Category);       // 分类来自目录，不靠 CSV 里那一列
            Assert.Equal(SecsFormat.U2, back.DataType);
            // 既有行为：Value 是 object，CSV 读回来一律是 string（SetPropertyValue 用 Convert.ChangeType
            // 转成 object 就只得到 string）。组包路径 Convert.ToUInt16 能吃下，所以不影响 S6F11。
            Assert.Equal((ushort)7, Convert.ToUInt16(back.Value));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 常用项被删掉后，下次 Load 按 ID 补回来；用户自己的行和改过的名不受影响。
    /// </summary>
    [Fact]
    public void Deleted_Protected_Items_Are_Restored_On_Next_Load()
    {
        var root = Path.Combine(Path.GetTempPath(), "secs_restore_" + Guid.NewGuid().ToString("N"));
        try
        {
            // SVID.csv 里删掉了 102/103，101 改了名，加了一行自己的 900
            WriteCsv(Path.Combine(root, "SVID"), "SVID.csv",
                "ID,Name,DataTypeName,Value\n101,MyClock,ASCII,\n900,MyVar,U2,1\n");
            // ReportID.csv 里删掉了 11；CEID.csv 里删掉了 102
            WriteCsv(Path.Combine(root, "ReportID"), "ReportID.csv",
                "ID,Name,VID_Collection\n1,Clock,101\n");
            WriteCsv(Path.Combine(root, "CEID"), "CEID.csv",
                "ID,Name,ReportID_Collection\n101,Device Status,\"1,13,12\"\n");

            var svid = new SVID { FilePath = Path.Combine(root, "SVID") };
            var report = new ReportID { FilePath = Path.Combine(root, "ReportID") };
            var ceid = new CEID { FilePath = Path.Combine(root, "CEID") };
            svid.Load();
            report.Load();
            ceid.Load();

            // 删掉的补回来了
            Assert.Equal(new[] { "101", "900", "102", "103" }, svid.Datas.Select(v => v.ID));
            Assert.Contains(report.Datas, r => r.ID == "11");
            Assert.Contains(ceid.Datas, c => c.ID == "102");
            // 用户改过的名没被覆盖
            Assert.Equal("MyClock", svid.Datas.Single(v => v.ID == "101").Name);
            // 补回来的项分类和格式都换算好了，直接能用
            var restored = svid.Datas.Single(v => v.ID == "102");
            Assert.Equal("SVID", restored.Category);
            Assert.Equal(SecsFormat.U2, restored.DataType);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        static void WriteCsv(string dir, string file, string content)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, file), content);
        }
    }

    /// <summary>每张表一个目录，互不干扰（原来 SVID/DVID/ECID 共用 SecsJson//VID）。</summary>
    [Fact]
    public void Each_Variable_Kind_Lives_In_Its_Own_Folder()
    {
        Assert.EndsWith("SVID", new SVID().FilePath);
        Assert.EndsWith("DVID", new DVID().FilePath);
        Assert.EndsWith("ECID", new ECID().FilePath);
    }
}
