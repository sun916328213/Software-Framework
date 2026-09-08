using DataManager.Excel.Definition;

namespace DataManager.Excel
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
