using Extension.Integration.Prism;
using Prism.Ioc;
using Prism.Modularity;
using Secs;
using SecsGem.Core;
using Secs.Manager;
using Secs.Order;
using SemiTool.Tool;
using System.Reflection;


namespace SecsGem.Runtime
{
    /// <summary>
    /// HelpTool模块类，负责注册HelpTool中的服务
    /// </summary>
    public class SecsGemModule : IModule
    {
        /// <summary>
        /// 注册模块中的服务
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 获取当前执行的程序集
            var assembly = typeof(SecsControl).Assembly;
            RegisterServicesEx.RegisterSingletonFromNamespace(assembly, containerRegistry, "Secs.Manager");

            containerRegistry.RegisterSingleton<FactoryCollection>();
            containerRegistry.RegisterSingleton<SecsMessageRegistry>(provider => provider.Resolve<FactoryCollection>());
            containerRegistry.RegisterSingleton<SecsControl>();
            containerRegistry.RegisterSingleton<SecsGemRuntime>(provider => provider.Resolve<SecsControl>());
            containerRegistry.RegisterSingleton<ISecsGemRuntime>(provider => provider.Resolve<SecsControl>());
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
