using DataManager.Excel.Definition;

namespace DataManager.Excel;

/// <summary>
/// P12 Core seam：Excel Provider 工厂契约。Manager 只经此接口创建具体实现，
/// 测试可注入 Fake/Stub；新增实现在 Runtime 注册/扩展，不修改 Controls。
/// </summary>
public interface IExcelProviderFactory
{
    /// <summary>按参数创建尚未注册的 Excel 实例（不负责注册到管理器）。</summary>
    IExcel Create(ExcelParam param);
}
