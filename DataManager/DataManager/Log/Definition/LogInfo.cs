using System.ComponentModel;

namespace DataManager.Log.Definition
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
    /// 2026-09-16 从 <c>Extension.Struct</c> 迁入：日志是 DataManager 的领域概念，
    /// 通用工具库 Extension 不应定义日志模型（原设计导致 <c>DataManager.Log.ILog</c> 的
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

        public LogInfo(string time, string info, ELogLevel level = ELogLevel.Information)
        {
            Time = time;
            Info = info;
            Level = level;
        }
    }

}
