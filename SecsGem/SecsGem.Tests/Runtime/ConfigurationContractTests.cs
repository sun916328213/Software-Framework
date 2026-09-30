using Secs.Variable;
using SecsGem.Core;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Runtime;

public sealed class ConfigurationContractTests
{
    [Fact]
    public void Report_And_Event_Definitions_RoundTrip_Through_Core_Contract()
    {
        var root = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = RuntimeTestFixture.Create(new InMemorySecsTransport());
            var catalog = runtime._VIDManager;
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

    [Fact]
    public async Task Core_Alarm_Report_Updates_Local_Alarm_State_And_Sends_S5F1()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        runtime._VIDManager.Alarm.Datas.Add(new AlarmInfo
        {
            ID = "11", Description = "Pressure", Code = 0
        });

        Assert.True(await runtime.ReportAlarmAsync(new SecsGemAlarm("11", "Pressure", 1)));
        Assert.Equal((byte)1, runtime._VIDManager.Alarm.GetDatas().Single().Code);
        Assert.Equal("S5F1", transport.LastSentOrder);
    }
}
