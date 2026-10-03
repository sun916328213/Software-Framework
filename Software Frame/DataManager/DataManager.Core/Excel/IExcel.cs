// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：IExcel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Excel.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Excel.Definition;

namespace DataManager.Core.Excel
{
    /// <summary>
    /// Excel 完整接口（P1-4：由 IExcelReader + IExcelWriter 组合，保持兼容）。
    /// 仅读场景请依赖 IExcelReader，仅写场景请依赖 IExcelWriter。
    /// </summary>
    public interface IExcel : IExcelReader, IExcelWriter
    {
        /// <summary>
        /// Excel参数
        /// </summary>
        ExcelParam ExcelParam { get; set; }
    }
}
