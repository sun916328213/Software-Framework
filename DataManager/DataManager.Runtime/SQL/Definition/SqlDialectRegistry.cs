namespace DataManager.SQL.Definition;

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

    public static ISqlDialect Get(ESQLType dbType) =>
        _dialects.TryGetValue(dbType, out var dialect)
            ? dialect
            : throw new NotSupportedException($"不支持的数据库类型: {dbType}");

    public static void Register(ISqlDialect dialect) => _dialects[dialect.DbType] = dialect;
}
