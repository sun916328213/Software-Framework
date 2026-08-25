using DataManager.SQL.Definition;
using Dapper;
using System;
using System.Data;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// SQL方言助手
    /// 统一处理不同数据库的连接字符串、SQL语法、数据类型映射
    /// 所有ESQLType相关的SQL方言逻辑都集中在此类中
    /// </summary>
    public static class SqlDialectHelper
    {
        #region 连接字符串相关

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        public static IDbConnection CreateConnection(ESQLType dbType, string connectionString)
        {
            return dbType switch
            {
                ESQLType.PostgreSQL => new Npgsql.NpgsqlConnection(connectionString),
                ESQLType.MySQL => new MySqlConnector.MySqlConnection(connectionString),
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };
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
            return sqlParam.DBType switch
            {
                ESQLType.PostgreSQL => $"Host={sqlParam.ServerIP};Port={sqlParam.Port};Database={dbName};Username={sqlParam.UserName};Password={sqlParam.Password}",
                ESQLType.MySQL => string.IsNullOrEmpty(dbName) 
                    ? $"Server={sqlParam.ServerIP};Port={sqlParam.Port};Uid={sqlParam.UserName};Pwd={sqlParam.Password}"
                    : $"Server={sqlParam.ServerIP};Port={sqlParam.Port};Database={dbName};Uid={sqlParam.UserName};Pwd={sqlParam.Password}",
                _ => throw new NotSupportedException($"不支持的数据库类型: {sqlParam.DBType}")
            };
        }

        #endregion

        #region 数据库操作相关

        /// <summary>
        /// 创建数据库SQL
        /// </summary>
        public static string BuildCreateDatabaseSql(string dbName, ESQLType dbType)
        {
            return dbType switch
            {
                ESQLType.PostgreSQL => $"CREATE DATABASE \"{dbName}\"",
                ESQLType.MySQL => $"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };
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
            var systemDbName = sqlParam.DBType == ESQLType.PostgreSQL ? "postgres" : "";
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
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };
        }

        /// <summary>
        /// 检查表是否存在的SQL
        /// </summary>
        public static string BuildCheckTableExistsSql(string tableName, string dbName, ESQLType dbType)
        {
            return dbType switch
            {
                ESQLType.PostgreSQL => $"SELECT 1 FROM information_schema.tables WHERE table_name = '{tableName}'",
                ESQLType.MySQL => $"SELECT 1 FROM information_schema.tables WHERE table_schema = '{dbName}' AND table_name = '{tableName}'",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };
        }

        /// <summary>
        /// 获取CREATE TABLE语句的后缀
        /// </summary>
        public static string GetCreateTableSuffix(ESQLType dbType)
        {
            return dbType switch
            {
                ESQLType.PostgreSQL => "",
                ESQLType.MySQL => " ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                _ => ""
            };
        }

        /// <summary>
        /// 包装表名（用于CREATE TABLE）
        /// </summary>
        public static string WrapTableName(string tableName, ESQLType dbType)
        {
            return dbType switch
            {
                ESQLType.PostgreSQL => tableName,
                ESQLType.MySQL => $"`{tableName}`",
                _ => tableName
            };
        }

        #endregion

        #region 数据类型映射

        /// <summary>
        /// 获取数据库字段类型
        /// </summary>
        public static string GetDbType(Type type, ESQLType dbType)
        {
            // 处理可空类型
            type = Nullable.GetUnderlyingType(type) ?? type;

            return dbType switch
            {
                ESQLType.PostgreSQL => type.Name switch
                {
                    "Int32" => "INTEGER",
                    "Int64" => "BIGINT",
                    "String" => "TEXT",
                    "DateTime" => "TIMESTAMP WITHOUT TIME ZONE",
                    "Boolean" => "BOOLEAN",
                    "Double" => "DOUBLE PRECISION",
                    "Single" => "REAL",
                    "Decimal" => "NUMERIC",
                    "Byte[]" => "BYTEA",
                    _ => "TEXT"
                },
                ESQLType.MySQL => type.Name switch
                {
                    "Int32" => "INT",
                    "Int64" => "BIGINT",
                    "String" => "TEXT",
                    "DateTime" => "DATETIME",
                    "Boolean" => "TINYINT(1)",
                    "Double" => "DOUBLE",
                    "Single" => "FLOAT",
                    "Decimal" => "DECIMAL(18,2)",
                    "Byte[]" => "LONGBLOB",
                    _ => "TEXT"
                },
                _ => "TEXT"
            };
        }

        /// <summary>
        /// 构建主键定义
        /// </summary>
        public static string BuildPrimaryKeyDefinition(string columnName, Type propertyType, ESQLType dbType)
        {
            var convertedName = SqlNamingHelper.ConvertColumnName(columnName, dbType);
            
            return dbType switch
            {
                ESQLType.PostgreSQL => (propertyType == typeof(long) || propertyType == typeof(int))
                    ? $"{convertedName} BIGSERIAL PRIMARY KEY"
                    : $"{convertedName} PRIMARY KEY",
                ESQLType.MySQL => (propertyType == typeof(long) || propertyType == typeof(int))
                    ? $"`{columnName}` BIGINT AUTO_INCREMENT PRIMARY KEY"
                    : $"`{columnName}` PRIMARY KEY",
                _ => $"{columnName} PRIMARY KEY"
            };
        }

        #endregion
    }
}
