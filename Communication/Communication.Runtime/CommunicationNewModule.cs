using CommunicationNew.Composition;
using CommunicationNew.Core;
using Prism.Ioc;
using Prism.Modularity;

namespace CommunicationNew;

/// <summary>把 CommunicationNew 默认组合根接入 Prism 宿主。</summary>
public sealed class CommunicationNewModule : IModule
{
    /// <inheritdoc />
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        var factory = CommunicationDefaults.CreateDefaultFactory();

        // Core 接口是宿主的新依赖入口，旧 Composition 接口继续解析到同一实例。
        containerRegistry.RegisterInstance<CommunicationNew.Core.ICommunicationDeviceFactory>(factory);
#pragma warning disable CS0618 // Prism 旧解析键在迁移期继续映射到同一 factory。
        containerRegistry.RegisterInstance<CommunicationNew.Composition.ICommunicationDeviceFactory>(
            (CommunicationNew.Composition.ICommunicationDeviceFactory)factory);
#pragma warning restore CS0618
        // P11：Controls 只依赖 Core 契约；新旧 manager 解析到同一单例。
        var manager = new CommunicationManager(factory);
        containerRegistry.RegisterInstance<CommunicationManager>(manager);
        containerRegistry.RegisterInstance<ICommunicationManager>(manager);
    }

    /// <inheritdoc />
    public void OnInitialized(IContainerProvider containerProvider)
    {
        // CommunicationNew 不在模块初始化阶段启动连接，由宿主显式管理设备生命周期。
    }
}
