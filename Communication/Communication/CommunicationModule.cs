using Communication.MC;
using Communication.Socket;
using DataManager.Log;
using DataManager.Log.Definition;
using DataManager.Log.SeriLog;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;


namespace Communication
{
    /// <summary>
    /// HelpTool模块类，负责注册HelpTool中的服务 
    /// </summary>
    public class CommunicationModule : IModule
    {
        /// <summary>
        /// 注册模块中的服务
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 获取当前执行的程序集
            var assembly = Assembly.GetExecutingAssembly();


            // 注册日志服务
            containerRegistry.RegisterSingleton<MCHelper>();
            containerRegistry.RegisterSingleton<CommunicationManager>();
            containerRegistry.RegisterSingleton<SocketHelper>();

            ILog log = new SerilogHelper();
            log.Param = new LogParam()
            {
                Name = "CommunicationLog",
                FilePath = @"D:\Log\CommunicationLog",
            };
            containerRegistry.RegisterInstance<ILog>(log);
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