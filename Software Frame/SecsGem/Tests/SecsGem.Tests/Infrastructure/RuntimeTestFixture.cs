// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RuntimeTestFixture。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls、SecsGem.Tests、DataManager.Core.Log、DataManager.Runtime.Log、SecsGem.Core、SecsGem.Runtime。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls;
using SecsGem.Tests;

using DataManager.Core.Log;
using DataManager.Runtime.Log;
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Runtime.Transport;
using SecsGem.Tests.Fakes;
using SecsGem.Runtime.Variable;
using SecsGem.Runtime.Variable.Data;

namespace SecsGem.Tests.Infrastructure;

/// <summary>
/// RuntimeTestFixture 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
internal static class RuntimeTestFixture
{
    /// <summary>
    /// 测试相关入口 Create；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterSingleton、RegisterTypes、RegisterInstance；具体条件和先后次序见方法体。
    /// </summary>
    internal static SecsGemRuntime Create(InMemorySecsTransport transport)
    {
        var container = NewContainer();
        var runtime = new SecsGemRuntime(container, transport);
        container.RegisterInstance(typeof(SecsGemRuntime), runtime);
        return runtime;
    }

    /// <summary>
    /// 用真实传输（Secs4NetTransport）装配运行时。
    /// <para>
    /// 与 <see cref="Create(InMemorySecsTransport)"/> 的区别：这里走真的 HSMS 套接字收发，
    /// 才能覆盖 secs4net 内部"报文在建通道之前被丢弃"的那段路径 —— 假传输把它整段绕开了。
    /// </para>
    /// </summary>
    internal static SecsGemRuntime CreateReal()
    {
        var container = NewContainer();
        var transport = new Secs4NetTransport(container);
        var runtime = new SecsGemRuntime(container, transport);
        container.RegisterInstance(typeof(SecsGemRuntime), runtime);
        return runtime;
    }

    private static TestPrismContainer NewContainer()
    {
        var container = new TestPrismContainer();
        container.RegisterSingleton(typeof(LogManager), () => new LogManager());
        foreach (var type in new[]
        {
            typeof(ReportID), typeof(CEID), typeof(DVID), typeof(SVID), typeof(ECID),
            typeof(Alarm), typeof(ActiveReceive)
        })
        {
            container.RegisterSingleton(type, type);
        }
        new SecsGemModule().RegisterTypes(container);
        return container;
    }
}
