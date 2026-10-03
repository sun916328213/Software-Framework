// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ISqlDialect。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、System.Data。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using System.Data;

namespace DataManager.Core.SQL.Definition
{
    /// <summary>
    /// P3-2：SQL 方言策略接口。新增数据库类型时实现本接口并在 <see cref="SqlDialectRegistry"/> 注册，
    /// 不再到各 Helper 里加 switch 分支。
    /// </summary>
    public interface ISqlDialect
    {
        ESQLType DbType { get; }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildConnectionString。
        /// </summary>
        string BuildConnectionString(SQLParam param, string? dbNameOverride = null);

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateConnection。
        /// </summary>
        IDbConnection CreateConnection(string connectionString);

        /// <summary>确保数据库存在（PG/MySQL/SQLServer=建库；SQLite=建目录，文件懒建）。</summary>
        void EnsureDatabase(SQLParam param);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCreateDatabaseSql。
        /// </summary>
        string BuildCreateDatabaseSql(string dbName);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCheckTableExistsSql。
        /// </summary>
        string BuildCheckTableExistsSql(string tableName, string dbName);
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetCreateTableSuffix。
        /// </summary>
        string GetCreateTableSuffix();
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteTable。
        /// </summary>
        string QuoteTable(string tableName);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteColumn。
        /// </summary>
        string QuoteColumn(string columnName);
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetColumnDbType。
        /// </summary>
        string GetColumnDbType(Type type);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildPrimaryKeyDefinition。
        /// </summary>
        string BuildPrimaryKeyDefinition(string columnName, Type propertyType);
        /// <summary>
        /// 分表 LIKE 查询（Dapper 口径：基表名统一小写；EFCore 因保留大小写自写 SQL，不用本方法）。
        /// </summary>
        string BuildSplitTablesLikeSql(string baseTableName, string dbName);
    }
}
