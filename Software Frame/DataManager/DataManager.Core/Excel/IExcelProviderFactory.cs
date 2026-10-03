// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：IExcelProviderFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Excel.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Excel.Definition;

namespace DataManager.Core.Excel;

/// <summary>
/// P12 Core seam：Excel Provider 工厂契约。Manager 只经此接口创建具体实现，
/// 测试可注入 Fake/Stub；新增实现在 Runtime 注册/扩展，不修改 Controls。
/// </summary>
public interface IExcelProviderFactory
{
    /// <summary>按参数创建尚未注册的 Excel 实例（不负责注册到管理器）。</summary>
    IExcel Create(ExcelParam param);
}
