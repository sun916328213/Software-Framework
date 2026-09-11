using Dapper;
using System;
using System.Data;

namespace DataManager.SQL.Definition
{
    /// <summary>
    /// SQL方言助手（P3-2：转调 SqlDialectRegistry，原有 API 保持兼容）。
    /// P15：从 DataManager.SQL.Dapper 搬到 Definition——P3 起即服务四库三工具，不再是 Dapper 专属。
    /// 新库请实现 ISqlDialect 并注册，不要再往这里加 switch。
    /// </summary>
    public static class SqlDialectHelper
    {
        #region 连接字符串相关

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        public static IDbConnection CreateConnection(ESQLType dbType, string connectionString)
        {
            return SqlDialectRegistry.Get(dbType).CreateConnection(connectionString);
        }

        /// <summary>
        /// 构建连接字符串
        /// </summary>
        public static string BuildConnectionString(SQLParam sqlParam)
        {
            return BuildConnectionString(sqlParam, sqlParam.DBName);
        }

        /// <summary>
        /// 构建连接字符串（指定数据库名）
        /// </summary>
        public static string BuildConnectionString(SQLParam sqlParam, string dbName)
        {
            if (!string.IsNullOrWhiteSpace(sqlParam.ConnectionString))
                return sqlParam.ConnectionString;
            if (sqlParam.DBType == ESQLType.SQLite && string.IsNullOrEmpty(dbName))
                return SqlDialectRegistry.Get(sqlParam.DBType).BuildConnectionString(sqlParam);
            return SqlDialectRegistry.Get(sqlParam.DBType).BuildConnectionString(sqlParam, dbName);
        }

        #endregion

        #region 数据库操作相关

        /// <summary>
        /// 创建数据库SQL
        /// </summary>
        public static string BuildCreateDatabaseSql(string dbName, ESQLType dbType)
        {
            return SqlDialectRegistry.Get(dbType).BuildCreateDatabaseSql(dbName);
        }

        /// <summary>
        /// 检查数据库是否存在
        /// </summary>
        public static bool IsDatabaseExists(SQLParam sqlParam)
        {
            try
            {
                using var conn = CreateConnectionToTarget(sqlParam);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 创建数据库（自动连接系统数据库）
        /// </summary>
        public static void CreateDatabase(SQLParam sqlParam)
        {
            var systemConnStr = BuildSystemConnectionString(sqlParam);
            using var conn = CreateConnection(sqlParam.DBType, systemConnStr);
            conn.Open();
            var sql = BuildCreateDatabaseSql(sqlParam.DBName, sqlParam.DBType);
            conn.Execute(sql);
        }

        /// <summary>
        /// 连接到目标数据库
        /// </summary>
        private static IDbConnection CreateConnectionToTarget(SQLParam sqlParam)
        {
            var connStr = BuildConnectionString(sqlParam);
            return CreateConnection(sqlParam.DBType, connStr);
        }

        /// <summary>
        /// 构建系统数据库连接字符串
        /// </summary>
        private static string BuildSystemConnectionString(SQLParam sqlParam)
        {
            // SQLite 无系统库概念，直连文件即可；其余走各方言的系统库约定。
            if (sqlParam.DBType == ESQLType.SQLite)
                return BuildConnectionString(sqlParam, sqlParam.EffectiveFilePath);
            var systemDbName = sqlParam.DBType switch
            {
                ESQLType.PostgreSQL => "postgres",
                ESQLType.SQL_Server => "master",
                _ => string.Empty,
            };
            return BuildConnectionString(sqlParam, systemDbName);
        }

        /// <summary>
        /// 检查数据库是否存在的SQL
        /// </summary>
        public static string BuildCheckDatabaseExistsSql(string dbName, ESQLType dbType)
        {
            return dbType switch
            {
                ESQLType.PostgreSQL => $"SELECT 1 FROM pg_database WHERE datname = '{dbName}'",
                ESQLType.MySQL => $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{dbName}'",
                ESQLType.SQLite => "SELECT 1",
                ESQLType.SQL_Server => $"SELECT 1 FROM sys.databases WHERE name = N'{dbName}'",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };
        }

        /// <summary>
        /// 检查表是否存在的SQL
        /// </summary>
        public static string BuildCheckTableExistsSql(string tableName, string dbName, ESQLType dbType)
        {
            return SqlDialectRegistry.Get(dbType).BuildCheckTableExistsSql(tableName, dbName);
        }

        /// <summary>
        /// 获取CREATE TABLE语句的后缀
        /// </summary>
        public static string GetCreateTableSuffix(ESQLType dbType)
        {
            return SqlDialectRegistry.Get(dbType).GetCreateTableSuffix();
        }

        /// <summary>
        /// 包装表名（用于CREATE TABLE）
        /// </summary>
        public static string WrapTableName(string tableName, ESQLType dbType)
        {
            return SqlDialectRegistry.Get(dbType).QuoteTable(tableName);
        }

        #endregion

        #region 数据类型映射

        /// <summary>
        /// 获取数据库字段类型
        /// </summary>
        public static string GetDbType(Type type, ESQLType dbType)
        {
            return SqlDialectRegistry.Get(dbType).GetColumnDbType(type);
        }

        /// <summary>
        /// 构建主键定义
        /// </summary>
        public static string BuildPrimaryKeyDefinition(string columnName, Type propertyType, ESQLType dbType)
        {
            return SqlDialectRegistry.Get(dbType).BuildPrimaryKeyDefinition(columnName, propertyType);
        }

        #endregion
    }
}
