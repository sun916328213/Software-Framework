// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ELogLevel、LogInfo。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log、System.ComponentModel。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Log;
using System.ComponentModel;

namespace DataManager.Core.Log.Definition
{
    /// <summary>
    /// Log日志等级
    /// </summary>
    public enum ELogLevel
    {
        [Description("信息")]
        Information,
        [Description("警告")]
        Warning,
        [Description("警告复位")]
        Reset,
        [Description("错误")]
        Error,
        [Description("系统错误")]
        SysError,
    }

    /// <summary>
    /// Log日志信息
    /// </summary>
    /// <remarks>
    /// 2026-09-16 从 <c>Extension.Contracts</c> 迁入：日志是 DataManager 的领域概念，
    /// 通用工具库 Extension 不应定义日志模型（原设计导致 <c>DataManager.Core.Log.ILog</c> 的
    /// 公开 API 反过来依赖 Extension 的类型，属依赖方向倒置）。
    ///
    /// <para>
    /// 同时移除了 <c>LogInfoDTO</c> 与 <c>LogInfoMapping</c>：DTO 唯一的用途是作为
    /// 日志查询界面 DataGrid 的 ItemsSource，而字段筛选、等级筛选、饼图统计、CSV 导出
    /// 全都基于本类型。DataGrid 为自动生成列，直接绑定本类型时 Level 走 <c>ToString()</c>，
    /// 与原 DTO 的 Level 字符串显示结果一致。
    /// </para>
    /// </remarks>
    public struct LogInfo
    {
        public string Time { get; set; }
        public string Info { get; set; }
        public ELogLevel Level { get; set; }

        /// <summary>
        /// 初始化 LogInfo 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public LogInfo(string time, string info, ELogLevel level = ELogLevel.Information)
        {
            Time = time;
            Info = info;
            Level = level;
        }
    }

}
