// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModuleRegistrationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Core、FlowEditor.Controls.Services、FlowEditor.Runtime、Prism.DryIoc、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Services;
using FlowEditor.Runtime;
using Prism.DryIoc;
using Prism.Ioc;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// ModuleRegistrationTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModuleRegistrationTests
{
    /// <summary>
    /// 验证 RepeatedRegistrationPreservesServicesAndHostReplacement 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterTypes、Resolve、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RepeatedRegistrationPreservesServicesAndHostReplacement()
    {
        var container = new DryIocContainerExtension();
        var context = new FlowLogExecutionContextAdapter();
        container.RegisterInstance<IExecutionLogContext>(context);
        new FlowEditorModule().RegisterTypes(container);
        var connections = container.Resolve<ConnectionService>();
        new FlowEditorModule().RegisterTypes(container);
        Assert.Same(context, container.Resolve<IExecutionLogContext>());
        Assert.Same(connections, container.Resolve<ConnectionService>());
    }

    /// <summary>
    /// 验证 DefaultServicesBelongToTheirContainer 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterTypes、NotSame、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultServicesBelongToTheirContainer()
    {
        var first = new DryIocContainerExtension();
        var second = new DryIocContainerExtension();
        new FlowEditorModule().RegisterTypes(first);
        new FlowEditorModule().RegisterTypes(second);
        Assert.NotSame(first.Resolve<ConnectionService>(), second.Resolve<ConnectionService>());
    }
}
