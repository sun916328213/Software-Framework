// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FeatureControlsModuleTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Controls、MotionControl.Runtime.Services、Prism.DryIoc、Prism.Ioc、Prism.Modularity、Vision.Controls。导入不代表所有类型都会在运行时加载。
using MotionControl.Controls;
using MotionControl.Runtime.Services;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Vision.Controls;
using Vision.Controls.Presentation;
using Vision.Core.Presentation;
using Xunit;

namespace WPFControls.Tests;

/// <summary>
/// FeatureControlsModuleTests 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("Prism module registration")]
public sealed class FeatureControlsModuleTests
{
    /// <summary>
    /// 验证 HostNavigationSurvivesRepeatedModuleRegistration 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterTypes、Same、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("MotionOverviewControl")]
    [InlineData("AxisStatusControl")]
    [InlineData("AxisJogControl")]
    [InlineData("AxisOperationControl")]
    [InlineData("ToolEditorView")]
    [InlineData("ToolCardView")]
    [InlineData("RoiEditorView")]
    [InlineData("SecsOverviewView")]
    [InlineData("ScriptManagerView")]
    [InlineData("FlowChartContainerView")]
    public void HostNavigationSurvivesRepeatedModuleRegistration(string name)
    {
        var container = new DryIocContainerExtension();
        var replacement = new object();
        container.RegisterInstance(typeof(object), replacement, name);
        IModule[] modules = [new MotionControlsModule(), new VisionControlsModule(),
            new SecsGem.Controls.SecsGemModule(), new ScriptEditor.Controls.ScriptEditorModule(),
            new FlowEditor.Controls.FlowEditorModule()];
        foreach (var module in modules)
        {
            module.RegisterTypes(container);
            module.RegisterTypes(container);
        }
        Assert.Same(replacement, container.Resolve<object>(name));
    }

    /// <summary>
    /// 验证 MotionAndVisionDefaultsAreDeferredAndBelongToTheirContainer 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterTypes、Resolve、Empty、Same、NotSame；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MotionAndVisionDefaultsAreDeferredAndBelongToTheirContainer()
    {
        var first = new DryIocContainerExtension();
        var second = new DryIocContainerExtension();
        foreach (var container in new[] { first, second })
        {
            new MotionControlsModule().RegisterTypes(container);
            new VisionControlsModule().RegisterTypes(container);
        }
        var manager = first.Resolve<MotionManager>();
        Assert.Empty(manager.Devices);
        new MotionControlsModule().RegisterTypes(first);
        Assert.Same(manager, first.Resolve<MotionManager>());
        Assert.NotSame(manager, second.Resolve<MotionManager>());
        Assert.Same(first.Resolve<IToolPresentation>(), first.Resolve<IToolPresentation>());
        Assert.NotSame(first.Resolve<IToolPresentation>(), second.Resolve<IToolPresentation>());
    }

    /// <summary>
    /// 验证 MotionAndVisionRespectHostServices 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterTypes、Same、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MotionAndVisionRespectHostServices()
    {
        var container = new DryIocContainerExtension();
        using var manager = new MotionManager();
        var presentation = new WpfToolPresentation(_ => { });
        container.RegisterInstance(manager);
        container.RegisterInstance<IToolPresentation>(presentation);
        new MotionControlsModule().RegisterTypes(container);
        new VisionControlsModule().RegisterTypes(container);
        Assert.Same(manager, container.Resolve<MotionManager>());
        Assert.Same(presentation, container.Resolve<IToolPresentation>());
    }
}
