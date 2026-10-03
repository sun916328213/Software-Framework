// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LogContractCompositionTests、RecordingLogManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core.Log、DataManager.Core.Log.Definition、DataManager.Runtime.Log、FlowEditor.Core、Prism.DryIoc。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using DataManager.Runtime.Log;
using FlowEditor.Core;
using Prism.DryIoc;
using Prism.Ioc;
using WPFControls.ProgramControl.Services;
using Xunit;

namespace WPFControls.Tests;

// Prism 的 ViewModelLocationProvider 使用进程级字典，模块注册测试必须串行。
[Collection("Prism module registration")]
public sealed class LogContractCompositionTests
{
    /// <summary>
    /// 验证 UiAndFlowLoggingUseHostContractWithoutResolvingDefaultManager 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterSingleton、RegisterTypes、AddRunInfo、Resolve；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    [Fact]
    public void UiAndFlowLoggingUseHostContractWithoutResolvingDefaultManager()
    {
        Exception? fault = null;
        var thread = new Thread(() =>
        {
            try
            {
                var replacement = new RecordingLogManager();
                var container = new DryIocContainerExtension();
                container.RegisterInstance<ILogManager>(replacement);
                container.RegisterSingleton<LogManager>(_ => throw new InvalidOperationException("Default manager must not be resolved."));
                new DataManagerModule().RegisterTypes(container);
                new WPFControlsModule().RegisterTypes(container);

                container.Resolve<LogInfoService>().AddRunInfo("contract probe");
                Assert.Contains("RunLog", replacement.Lookups);
                Assert.Same(replacement, container.Resolve<ILogManager>());
                container.Resolve<IFlowLogSink>().Write(new FlowLogRecord("process", "flow", "node", 1, "probe", FlowLogLevel.Information));
                Assert.Contains("FlowLog", replacement.Lookups);
            }
            catch (Exception ex) { fault = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        Assert.Null(fault);
    }

    /// <summary>
    /// RecordingLogManager 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILogManager。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class RecordingLogManager : ILogManager
    {
        public List<string> Lookups { get; } = [];
        public IReadOnlyList<ILog> Items => [];
        public int Count => 0;
        /// <summary>
        /// 验证 TryGet 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGet(string name, out ILog? item) { Lookups.Add(name); item = null; return false; }
        /// <summary>
        /// 验证 ReplaceConfigured 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void ReplaceConfigured(IReadOnlyCollection<string> ownedNames, IReadOnlyList<LogParam> parameters) { }
        /// <summary>
        /// 验证 Config 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void Config(List<LogParam> parameters) => throw new NotSupportedException();
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ILog Create(LogParam parameter) => throw new NotSupportedException();
        /// <summary>
        /// 验证 Add 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void Add(ILog item) => throw new NotSupportedException();
        /// <summary>
        /// 验证 Remove 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public bool Remove(ILog item) => throw new NotSupportedException();
        /// <summary>
        /// 验证 Clear 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void Clear() => throw new NotSupportedException();
    }
}
