// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：CommunicationModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Runtime.Composition、Communication.Runtime.Core、Communication.Controls.Services、Communication.Controls.ViewModels、Communication.Controls.Views。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Runtime.Composition;
using Communication.Runtime.Core;
using Communication.Controls.Services;
using Communication.Controls.ViewModels;
using Communication.Controls.Views;
using Extension.Integration.Prism;
using Prism.Ioc;
using Prism.Modularity;

namespace Communication.Controls;

/// <summary>Registers communication runtime defaults and UI without opening connections.</summary>
/// <remarks>
/// CommunicationModule 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：IModule。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class CommunicationModule : IModule
{
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// </summary>
    public void OnInitialized(IContainerProvider provider) { }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 ThrowIfNull、IsRegistered、CreateDefaultFactory、RegisterInstance、RegisterSingleton；具体条件和先后次序见方法体。
    /// </summary>
    public void RegisterTypes(IContainerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.IsRegistered<Communication.Core.ICommunicationDeviceFactory>())
        {
            var factory = CommunicationDefaults.CreateDefaultFactory();
            registry.RegisterInstance<Communication.Core.ICommunicationDeviceFactory>(factory);

        }
        if (!registry.IsRegistered<ICommunicationManager>())
        {
            if (!registry.IsRegistered<CommunicationManager>())
                registry.RegisterSingleton<CommunicationManager>(provider => new CommunicationManager(provider.Resolve<Communication.Core.ICommunicationDeviceFactory>()));
            registry.RegisterSingleton<ICommunicationManager>(provider => provider.Resolve<CommunicationManager>());
        }
        if (!registry.IsRegistered<ILogService>()) registry.RegisterSingleton<ILogService, LogService>();
        if (!registry.IsRegistered<CommunicationSessionService>()) registry.RegisterSingleton<CommunicationSessionService>();
        if (!registry.IsRegistered(typeof(object), nameof(CommunicationView)))
        {
            registry.RegisterSingleton(typeof(object), typeof(CommunicationView), nameof(CommunicationView));
            Prism.Mvvm.ViewModelLocationProvider.Register<CommunicationView, CommunicationViewModel>();
        }
        if (!registry.IsRegistered(typeof(object), nameof(DeviceConfigDialog)))
            registry.RegisterForNavigation<DeviceConfigDialog, DeviceConfigDialogViewModel>();
    }
}
