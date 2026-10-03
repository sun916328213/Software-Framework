// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：MotionControlsModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Controls.Views、MotionControl.Controls.Services、MotionControl.Controls.ViewModels、MotionControl.Runtime.Services、Prism.Ioc、Prism.Modularity。导入不代表所有类型都会在运行时加载。
using MotionControl.Controls.Views;
using MotionControl.Controls.Services;
using MotionControl.Controls.ViewModels;
using MotionControl.Runtime.Services;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;

namespace MotionControl.Controls;

/// <summary>Registers motion services and independent controls without configuring or connecting cards.</summary>
/// <remarks>
/// MotionControlsModule 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：IModule。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class MotionControlsModule : IModule
{
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// </summary>
    public void OnInitialized(IContainerProvider provider) { }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 ThrowIfNull、IsRegistered、RegisterSingleton、Resolve、Register；具体条件和先后次序见方法体。
    /// </summary>
    public void RegisterTypes(IContainerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.IsRegistered<MotionManager>()) registry.RegisterSingleton<MotionManager>();
        if (!registry.IsRegistered<MotionWorkspace>())
            registry.RegisterSingleton<MotionWorkspace>(provider => new MotionWorkspace(provider.Resolve<MotionManager>()));
        if (!registry.IsRegistered<MotionViewModel>()) registry.Register<MotionViewModel>();
        ViewModelLocationProvider.Register<MotionView, MotionViewModel>();
        if (!registry.IsRegistered(typeof(object), nameof(MotionView))) registry.RegisterForNavigation<MotionView, MotionViewModel>();
        RegisterControl<MotionOverviewControl>(registry);
        RegisterControl<AxisStatusControl>(registry);
        RegisterControl<AxisJogControl>(registry);
        RegisterControl<AxisOperationControl>(registry);
    }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterControl。
    /// 正文中的关键协作调用包括 IsRegistered、Register、RegisterForNavigation；具体条件和先后次序见方法体。
    /// </summary>
    private static void RegisterControl<T>(IContainerRegistry registry)
    {
        // Card/axis bindings belong to each placement, so controls cannot be shared singletons.
        if (!registry.IsRegistered<T>()) registry.Register<T>();
        if (!registry.IsRegistered(typeof(object), typeof(T).Name)) registry.RegisterForNavigation<T>();
    }
}
