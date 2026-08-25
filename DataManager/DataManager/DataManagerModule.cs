using DataManager.Excel;
using DataManager.Log;
using DataManager.Log.Definition;
using DataManager.Log.SeriLog;
using DataManager.SQL;
using Extension.Core.ReflectionEx;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;


namespace DataManager
{
    /// <summary>
    /// HelpTool模块类，负责注册HelpTool中的服务
    /// </summary>
    public class DataManagerModule : IModule
    {
        /// <summary>
        /// 注册模块中的服务
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<LogManager>( );
            containerRegistry.RegisterSingleton<SQLManager>();
            containerRegistry.RegisterSingleton<ExcelManager>();
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