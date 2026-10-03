// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SqlDialectRegistry。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL;
namespace DataManager.Runtime.SQL.Definition;

/// <summary>
/// P12：方言注册表归属 Runtime（预置的四个方言实现均依赖真实 Provider 包）。
/// Core 只保留 <see cref="ISqlDialect"/> 契约；命名空间保持不变，避免调用方改 using。
/// </summary>
public static class SqlDialectRegistry
{
    private static readonly Dictionary<ESQLType, ISqlDialect> _dialects = new()
    {
        [ESQLType.PostgreSQL] = new PostgresDialect(),
        [ESQLType.MySQL] = new MySqlDialect(),
        [ESQLType.SQLite] = new SqliteDialect(),
        [ESQLType.SQL_Server] = new SqlServerDialect(),
    };

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 Get。
    /// 正文中的关键协作调用包括 TryGetValue；具体条件和先后次序见方法体。
    /// </summary>
    public static ISqlDialect Get(ESQLType dbType) =>
        _dialects.TryGetValue(dbType, out var dialect)
            ? dialect
            : throw new NotSupportedException($"不支持的数据库类型: {dbType}");

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 Register。
    /// </summary>
    public static void Register(ISqlDialect dialect) => _dialects[dialect.DbType] = dialect;
}
