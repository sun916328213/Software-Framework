// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ExcelProviderFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Excel、DataManager.Core.Excel.Definition、DataManager.Runtime.Excel.EPPlus、DataManager.Runtime.Excel.MiniExcel。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Excel;
using DataManager.Core.Excel.Definition;
using DataManager.Runtime.Excel.EPPlus;
using DataManager.Runtime.Excel.MiniExcel;

namespace DataManager.Runtime.Excel;

/// <summary>
/// P12 Runtime 内建 Excel Provider 工厂：按工具类型创建具体实现（不负责注册）。
/// 新增实现在此注册/扩展，不修改 Controls。
/// </summary>
public sealed class ExcelProviderFactory : IExcelProviderFactory
{
    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 ThrowIfNull、Validate；具体条件和先后次序见方法体。
    /// </remarks>
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
