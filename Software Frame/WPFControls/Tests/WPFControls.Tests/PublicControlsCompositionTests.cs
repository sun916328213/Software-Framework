// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PublicControlsCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DryIoc、Extension.Core.Reflection、Prism.DryIoc、WPFControls.Controls、WPFControls.Charts.Models、WPFControls.Controls.Views。导入不代表所有类型都会在运行时加载。
using DryIoc;
using Extension.Core.Reflection;
using Prism.DryIoc;
using WPFControls.Controls;
using WPFControls.Charts.Models;
using WPFControls.Controls.Views;
using Xunit;

namespace WPFControls.Tests;

/// <summary>
/// PublicControlsCompositionTests 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("Prism module registration")]
public sealed class PublicControlsCompositionTests
{
    /// <summary>
    /// 测试相关入口 PublicModuleUsesOnlyNamedNavigationRegistration；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterTypes、Single、GetServiceRegistrations、Equals、nameof；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PublicModuleUsesOnlyNamedNavigationRegistration()
    {
        var container = new DryIocContainerExtension();
        new WPFControlsModule().RegisterTypes(container);
        new WPFControlsModule().RegisterTypes(container);
        var registration = Assert.Single(container.Instance.GetServiceRegistrations(),
            entry => entry.ServiceType == typeof(object) && Equals(entry.OptionalServiceKey, nameof(PaginationView)));
        Assert.Equal(typeof(PaginationView), registration.ImplementationType);
        Assert.DoesNotContain(container.Instance.GetServiceRegistrations(),
            entry => entry.ServiceType == typeof(PaginationView));
    }

    /// <summary>
    /// 测试相关入口 HistoricalTypeNamesResolveAfterAssemblySplit；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Same、ResolvePersistedType；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("WPFControl.CustomControl.Models.Charts.PieData, WPFControl", typeof(ChartPieData))]
    [InlineData("WPFControl.CustomControl.Views.PaginationView, WPFControl", typeof(PaginationView))]
    [InlineData("WPFControl.CustomControl.Views.Display.DisplayQueryView, WPFControl", typeof(WPFControls.ProgramControl.Views.Display.DisplayQueryView))]
    [InlineData("WPFControl.CustomControl.Views.PrintView, WPFControl", typeof(WPFControls.ProgramControl.Views.PrintView))]
    public void HistoricalTypeNamesResolveAfterAssemblySplit(string name, Type expected)
    {
        Assert.Same(expected, FrameworkTypeResolver.ResolvePersistedType(name));
    }
}
