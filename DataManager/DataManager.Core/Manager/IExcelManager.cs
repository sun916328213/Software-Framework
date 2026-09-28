using DataManager.Excel.Definition;

namespace DataManager.Excel;

/// <summary>
/// P12 Core seam：Excel 管理器契约。Controls 只依赖此接口，不持有具体 <c>ExcelManager</c>。
/// </summary>
public interface IExcelManager : IManager<IExcel, ExcelParam>
{
}
