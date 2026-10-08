// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ProductionPortTests、Log、Plc。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.Log.Definition、MachineApp.Business.Ports、MachineApp.Business.Station、WPFControls.Controls.Models、Xunit。导入不代表所有类型都会在运行时加载。
using DataManager.Core.Log.Definition;
using MachineApp.Business.Ports;
using MachineApp.Business.Station;
using WPFControls.Controls.Models;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// ProductionPortTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ProductionPortTests
{
    /// <summary>
    /// 测试相关入口 StationAcceptsIndependentPlcAndLogImplementationsWithoutProductionServices；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Same、Refresh、AddRunInfo、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StationAcceptsIndependentPlcAndLogImplementationsWithoutProductionServices()
    {
        var plc = new Plc();
        var log = new Log();
        var logLookups = 0;
        var services = new StationServices(() => { logLookups++; return log; },
            () => null!, () => null!, () => null!, () => null!, () => plc);
        var station = new StationBase<int>(services);
        Assert.Equal(0, logLookups);
        Assert.Same(plc, station._PLCMethod);
        station._PLCMethod.Refresh();
        station._LogInfoService.AddRunInfo("station");
        Assert.Equal(1, plc.RefreshCount);
        Assert.Equal(1, logLookups);
        Assert.Equal("station", Assert.Single(log.Messages));
    }

    /// <summary>
    /// Log 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IProductionLog。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Log : IProductionLog
    {
        public List<string> Messages = [];
        /// <summary>
        /// 测试相关入口 AddRunInfo；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void AddRunInfo(string info, ELogLevel level = ELogLevel.Information, int retainNum = 1000) => Messages.Add(info);
    }

    /// <summary>
    /// Plc 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IProductionPlc。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Plc : IProductionPlc
    {
        public int RefreshCount;
        public bool IsOnline => false;
        /// <summary>
        /// 测试相关入口 Refresh；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Refresh() => RefreshCount++;
        /// <summary>
        /// 测试相关入口 StopRefresh；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void StopRefresh() => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 TriggerReply；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void TriggerReply(string stationName, string pPLCOrder) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 SendData；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SendData(string stationName, string pPLCOrder, int offset, object info) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 SendResult；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SendResult(string stationName, string pPLCOrder, EPLCResult info) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 ReadData；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public string ReadData(string stationName, string pPLCOrder, int offset = 0, bool islog = true) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 TriggerUserInfo；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void TriggerUserInfo(UserInfo value) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 TriggerAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task<EPLCResult> TriggerAsync(string stationName, string pPLCOrder, string data) => throw new NotSupportedException();
    }
}
