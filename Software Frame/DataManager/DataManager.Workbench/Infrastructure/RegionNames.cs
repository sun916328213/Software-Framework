// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：RegionNames。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Workbench。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Workbench;
namespace DataManager.Workbench.Infrastructure
{
    /// <summary>
    /// RegionNames 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
    /// </summary>
    public static class RegionNames
    {
        public const string MainRegion = "MainRegion";
    }
}
