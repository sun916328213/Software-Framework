using DataManager.Excel;
using DataManager.Log;
using DataManager.SQL;
using Prism.Ioc;
using Prism.Modularity;


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
            containerRegistry.RegisterSingleton<LogManager>();
            containerRegistry.RegisterSingleton<SQLManager>();
            containerRegistry.RegisterSingleton<ExcelManager>();
            // 说明：IExcel 实例必须带 ExcelParam（文件路径等）经 ExcelManager.Create 创建，
            // 此处不再注册无参 IExcel 单例（旧注册解析后 ExcelParam 为空，调用即 NRE）。
        }

        /// <summary>
        /// 模块初始化完成后的回调
        /// </summary>
        /// <param name="containerProvider">容器提供者</param>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            ApplyNpgsqlLegacyTimestampBehavior();
        }

        /// <summary>
        /// P2-4：Npgsql 时间戳兼容开关（原先是 OnInitialized 里的一行魔法代码，现显式命名+文档化）。
        /// 原因：Npgsql 6.0+ 默认拒绝写入 Local(DateTime.Now) 时间戳并抛错；
        /// 本库 EFCore/Dapper/SQL 测试大量使用 DateTime.Now/Today，保持 Legacy 行为可避免全量改 UTC。
        /// 如未来统一用 UTC 存储，在此删除调用并将实体时间改为 DateTime.UtcNow 即可。
        /// </summary>
        public static void ApplyNpgsqlLegacyTimestampBehavior()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }
    }
}