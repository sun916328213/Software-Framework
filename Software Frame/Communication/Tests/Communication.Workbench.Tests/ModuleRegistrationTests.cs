// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModuleRegistrationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Controls、Communication.Core、Communication.Runtime.Composition、Communication.Runtime.Core、Prism.DryIoc、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using Communication.Controls;
using Communication.Core;
using Communication.Runtime.Composition;
using Communication.Runtime.Core;
using Prism.DryIoc;
using Prism.Ioc;
using Xunit;

namespace Communication.Workbench.Tests;

/// <summary>
/// ModuleRegistrationTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModuleRegistrationTests
{
    /// <summary>
    /// 验证 HostNavigationReplacementSurvivesRepeatedRegistration 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterTypes、Same、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("CommunicationView")]
    [InlineData("DeviceConfigDialog")]
    public void HostNavigationReplacementSurvivesRepeatedRegistration(string navigationName)
    {
        var container = new DryIocContainerExtension();
        var replacement = new object();
        container.RegisterInstance(typeof(object), replacement, navigationName);
        var module = new CommunicationModule();
        module.RegisterTypes(container);
        Assert.Same(replacement, container.Resolve<object>(navigationName));
        module.RegisterTypes(container);
        Assert.Same(replacement, container.Resolve<object>(navigationName));
    }

    /// <summary>
    /// 验证 RepeatedRegistrationPreservesManagerAndFactoryInstances 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateDefaultFactory、RegisterInstance、RegisterTypes、Resolve、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RepeatedRegistrationPreservesManagerAndFactoryInstances()
    {
        var container = new DryIocContainerExtension();
        var factory = CommunicationDefaults.CreateDefaultFactory();
        container.RegisterInstance<Communication.Core.ICommunicationDeviceFactory>(factory);
        new CommunicationModule().RegisterTypes(container);
        var manager = container.Resolve<ICommunicationManager>();
        new CommunicationModule().RegisterTypes(container);
        Assert.Same(factory, container.Resolve<Communication.Core.ICommunicationDeviceFactory>());
        Assert.Same(manager, container.Resolve<ICommunicationManager>());
        Assert.Same(manager, container.Resolve<CommunicationManager>());
    }

    /// <summary>
    /// 验证 HostManagerReplacementIsPreserved 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateDefaultFactory、RegisterInstance、RegisterTypes、Same、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HostManagerReplacementIsPreserved()
    {
        var container = new DryIocContainerExtension();
        var manager = new CommunicationManager(CommunicationDefaults.CreateDefaultFactory());
        container.RegisterInstance<ICommunicationManager>(manager);
        new CommunicationModule().RegisterTypes(container);
        Assert.Same(manager, container.Resolve<ICommunicationManager>());
    }
}
