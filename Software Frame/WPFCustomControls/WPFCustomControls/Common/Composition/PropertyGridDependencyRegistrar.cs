// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：PropertyGridDependencyRegistrar。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、Prism.Ioc、WPFCustomControls.PropertyGrid.Core.Composition、WPFCustomControls.PropertyGrid.Core.Models、WPFCustomControls.PropertyGrid.Core.Services、WPFCustomControls.PropertyGrid.Services。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using Prism.Ioc;
using WPFCustomControls.PropertyGrid.Core.Composition;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.Services;
using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.Core.ViewModels;
using WPFCustomControls.PropertyGrid.Wpf;

namespace WPFCustomControls.Common.Composition;

/// <summary>Registers PropertyGrid Core interfaces and their WPF adapters.</summary>
/// <remarks>
/// PropertyGridDependencyRegistrar 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：IDependencyRegistrar。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class PropertyGridDependencyRegistrar : IDependencyRegistrar
{
    public string Name => "PropertyGrid";

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 Register。
    /// 正文中的关键协作调用包括 IsRegistered、RegisterInstance、CreateForCurrentApplication、RegisterSingleton、Register；具体条件和先后次序见方法体。
    /// </summary>
    public void Register(IContainerRegistry registry)
    {
        if (!registry.IsRegistered<IUiDispatcher>())
            registry.RegisterInstance<IUiDispatcher>(WpfUiDispatcher.CreateForCurrentApplication());
        if (!registry.IsRegistered<IPropertyGridBuilderService>())
            registry.RegisterSingleton<IPropertyGridBuilderService, PropertyGridBuilderService>();
        if (!registry.IsRegistered<IPortDialogService>())
            registry.RegisterSingleton<IPortDialogService, PortDialogService>();
        if (!registry.IsRegistered<PropertyGridHostViewModel>())
            registry.Register<PropertyGridHostViewModel>();
    }
}
