// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SqlProviderFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Runtime.SQL.EFCore;
using DataManager.Runtime.SQL.SqlSugar;

namespace DataManager.Runtime.SQL;

/// <summary>
/// P12 Runtime 内建 SQL Provider 工厂：按工具类型创建具体 Provider（不负责注册）。
/// 新增 Provider 在此注册/扩展，不修改 Controls。
/// </summary>
public sealed class SqlProviderFactory : ISqlProviderFactory
{
    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 ThrowIfNull、Validate、Create；具体条件和先后次序见方法体。
    /// </remarks>
    public ISQL<T> Create<T>(SQLParam param) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(param);
        param.Validate();
        return param.ToolType switch
        {
            ESQLToolType.EFCore => EFCoreHelper<T>.Create(param),
            ESQLToolType.SqlSugar => SqlSugarHelper<T>.Create(param),
            ESQLToolType.Dapper => DapperHelper<T>.Create(param),
            _ => throw new NotSupportedException($"{param.ToolType} not support.")
        };
    }
}
