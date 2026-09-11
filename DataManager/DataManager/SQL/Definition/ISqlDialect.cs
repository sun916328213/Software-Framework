using System.Data;

namespace DataManager.SQL.Definition
{
    /// <summary>
    /// P3-2：SQL 方言策略接口。新增数据库类型时实现本接口并在 <see cref="SqlDialectRegistry"/> 注册，
    /// 不再到各 Helper 里加 switch 分支。
    /// </summary>
    public interface ISqlDialect
    {
        ESQLType DbType { get; }

        string BuildConnectionString(SQLParam param, string? dbNameOverride = null);

        IDbConnection CreateConnection(string connectionString);

        /// <summary>确保数据库存在（PG/MySQL/SQLServer=建库；SQLite=建目录，文件懒建）。</summary>
        void EnsureDatabase(SQLParam param);

        string BuildCreateDatabaseSql(string dbName);
        string BuildCheckTableExistsSql(string tableName, string dbName);
        string GetCreateTableSuffix();
        string QuoteTable(string tableName);
        string QuoteColumn(string columnName);
        string GetColumnDbType(Type type);
        string BuildPrimaryKeyDefinition(string columnName, Type propertyType);
        /// <summary>
        /// 分表 LIKE 查询（Dapper 口径：基表名统一小写；EFCore 因保留大小写自写 SQL，不用本方法）。
        /// </summary>
        string BuildSplitTablesLikeSql(string baseTableName, string dbName);
    }

    /// <summary>
    /// 方言注册表：四库单例常驻，按 ESQLType 路由。
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
}
