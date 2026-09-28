using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;

namespace DataManager.SQL;

/// <summary>
/// P12 Runtime 内建 SQL Provider 工厂：按工具类型创建具体 Provider（不负责注册）。
/// 新增 Provider 在此注册/扩展，不修改 Controls。
/// </summary>
public sealed class SqlProviderFactory : ISqlProviderFactory
{
    /// <inheritdoc />
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
