// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：SecsGemModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、Prism.Ioc、Prism.Modularity、SecsGem.Controls、SecsGem.Core、SecsGem.Runtime.Manager。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using Prism.Ioc;
using Prism.Modularity;
using SecsGem.Controls;
using SecsGem.Core;
using SecsGem.Runtime.Manager;
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using SecsGem.Runtime.Protocol.Registry;
using SecsGem.Runtime.Variable;
using SecsGem.Runtime.Variable.Data;
using System.Reflection;


namespace SecsGem.Controls
{
    /// <summary>
    /// Registers SECS/GEM runtime defaults and reusable overview navigation.
    /// </summary>
    /// <remarks>
    /// SecsGemModule 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
    /// 继承或实现的契约：IModule。具体约束按这些契约及各成员实现解释。
    /// </remarks>
    public partial class SecsGemModule : IModule
    {
        // Non-WPF protocol fixtures link this composition file; UI registration lives in the Controls partial.
        /// <summary>
        /// 注册类型、服务或入口映射；对应入口为 RegisterViews。
        /// </summary>
        partial void RegisterViews(IContainerRegistry registry);
        /// <summary>
        /// 注册模块中的服务
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>

        /// <summary>Registers shared protocol state and runtime defaults without connecting.</summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            ArgumentNullException.ThrowIfNull(containerRegistry);
            RegisterViews(containerRegistry);
            if (!containerRegistry.IsRegistered<ReportID>()) containerRegistry.RegisterSingleton<ReportID>();
            if (!containerRegistry.IsRegistered<CEID>()) containerRegistry.RegisterSingleton<CEID>();
            if (!containerRegistry.IsRegistered<DVID>()) containerRegistry.RegisterSingleton<DVID>();
            if (!containerRegistry.IsRegistered<SVID>()) containerRegistry.RegisterSingleton<SVID>();
            if (!containerRegistry.IsRegistered<ECID>()) containerRegistry.RegisterSingleton<ECID>();
            if (!containerRegistry.IsRegistered<Alarm>()) containerRegistry.RegisterSingleton<Alarm>();
            if (!containerRegistry.IsRegistered<ActiveReceive>()) containerRegistry.RegisterSingleton<ActiveReceive>();

            if (!containerRegistry.IsRegistered<TraceManager>()) containerRegistry.RegisterSingleton<TraceManager>();
            if (!containerRegistry.IsRegistered<DeviceManager>()) containerRegistry.RegisterSingleton<DeviceManager>();
            if (!containerRegistry.IsRegistered<ConnectManager>()) containerRegistry.RegisterSingleton<ConnectManager>();
            if (!containerRegistry.IsRegistered<StatusManager>()) containerRegistry.RegisterSingleton<StatusManager>();
            if (!containerRegistry.IsRegistered<VIDManager>()) containerRegistry.RegisterSingleton<VIDManager>();

            if (!containerRegistry.IsRegistered<SecsMessageRegistry>()) containerRegistry.RegisterSingleton<SecsMessageRegistry>();
            if (!containerRegistry.IsRegistered<SecsGemRuntime>()) containerRegistry.RegisterSingleton<SecsGemRuntime>();
            if (!containerRegistry.IsRegistered<ISecsGemRuntime>()) containerRegistry.RegisterSingleton<ISecsGemRuntime>(provider => provider.Resolve<SecsGemRuntime>());
            if (!containerRegistry.IsRegistered<ISecsGemConfiguration>()) containerRegistry.RegisterSingleton<ISecsGemConfiguration>(provider => provider.Resolve<ISecsGemRuntime>() is ISecsGemConfiguration configuration ? configuration : throw new InvalidOperationException("Custom ISecsGemRuntime must provide ISecsGemConfiguration."));
        }
        /// <summary>
        /// 模块初始化完成后的回调
        /// </summary>
        /// <param name="containerProvider">容器提供者</param>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 在这里执行模块初始化完成后的操作
            // 例如获取服务实例等

        }
    }
}
