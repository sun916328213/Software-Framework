using DataManager.Excel.Definition;
using DataManager.Excel.EPPlus;
using DataManager.Excel.MiniExcel;

namespace DataManager.Excel;

/// <summary>
/// P12 Runtime 内建 Excel Provider 工厂：按工具类型创建具体实现（不负责注册）。
/// 新增实现在此注册/扩展，不修改 Controls。
/// </summary>
public sealed class ExcelProviderFactory : IExcelProviderFactory
{
    /// <inheritdoc />
    public IExcel Create(ExcelParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        param.Validate();
        return param.ToolType switch
        {
            ExcelType.MiniExcel => new MiniExcelHelper() { ExcelParam = param },
            ExcelType.EPPlus => new EPPlusHelper() { ExcelParam = param },
            _ => throw new NotSupportedException($"{param.ToolType} not support."),
        };
    }
}
