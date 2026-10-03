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
    /// 验证 Report_And_Event_Definitions_RoundTrip_Through_Core_Contract 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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

    /// <summary>
    /// 验证 Core_Alarm_Report_Updates_Local_Alarm_State_And_Sends_S5F1 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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
