using Extension.Core.Reflection;
using Prism.Ioc;
using Prism.Modularity;
using Secs;
using Secs.Manager;
using Secs.Order;
using Secs.UI;
using System.Reflection;


namespace Secs
{
    /// <summary>
    /// HelpTool模块类，负责注册HelpTool中的服务
    /// </summary>
    public class SecsToolModule : IModule
    {
        /// <summary>
        /// 注册模块中的服务
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 获取当前执行的程序集
            var assembly = Assembly.GetExecutingAssembly();
            RegisterServicesEx.RegisterSingletonFromNamespace(assembly, containerRegistry, "Secs.Manager");

            containerRegistry.RegisterSingleton<SecsControl>();
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
