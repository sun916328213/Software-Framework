// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：DialectShared、PostgresDialect、MySqlDialect、SqliteDialect、SqlServerDialect。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、Dapper、DataManager.Core.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System.Data;

namespace DataManager.Runtime.SQL.Definition
{
    /// <summary>
    /// DialectShared 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// </summary>
    internal static class DialectShared
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Unwrap。
        /// 正文中的关键协作调用包括 GetUnderlyingType；具体条件和先后次序见方法体。
        /// </summary>
        internal static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteIdent。
        /// </summary>
        internal static string QuoteIdent(string name, string open, string close) => $"{open}{name}{close}";
    }

    /// <summary>PostgreSQL 方言（原 SqlDialectHelper PG 分支整体搬迁）。</summary>
    public sealed class PostgresDialect : ISqlDialect
    {
        public ESQLType DbType => ESQLType.PostgreSQL;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildConnectionString。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildConnectionString(SQLParam param, string? dbNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(param.ConnectionString))
                return param.ConnectionString;
            var db = dbNameOverride ?? param.DBName;
            return $"Host={param.ServerIP};Port={param.Port};Database={db};Username={param.UserName};Password={param.Password}";
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateConnection。
        /// </summary>
        public IDbConnection CreateConnection(string connectionString) =>
            new Npgsql.NpgsqlConnection(connectionString);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsureDatabase。
        /// 正文中的关键协作调用包括 IsDatabaseExists、CreateConnection、BuildConnectionString、Open、Execute；具体条件和先后次序见方法体。
        /// </summary>
        public void EnsureDatabase(SQLParam param)
        {
            if (IsDatabaseExists(param)) return;
            using var conn = CreateConnection(BuildConnectionString(param, "postgres"));
            conn.Open();
            conn.Execute(BuildCreateDatabaseSql(param.DBName));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsDatabaseExists。
        /// 正文中的关键协作调用包括 Open；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static bool IsDatabaseExists(SQLParam param)
        {
            try
            {
                using var conn = new Npgsql.NpgsqlConnection(
                    $"Host={param.ServerIP};Port={param.Port};Database={param.DBName};Username={param.UserName};Password={param.Password}");
                conn.Open();
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCreateDatabaseSql。
        /// </summary>
        public string BuildCreateDatabaseSql(string dbName) => $"CREATE DATABASE \"{dbName}\"";
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCheckTableExistsSql。
        /// </summary>
        public string BuildCheckTableExistsSql(string tableName, string dbName) =>
            $"SELECT 1 FROM information_schema.tables WHERE table_name = '{tableName}'";
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetCreateTableSuffix。
        /// </summary>
        public string GetCreateTableSuffix() => string.Empty;
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteTable。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string QuoteTable(string tableName) => tableName.ToLowerInvariant();
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteColumn。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string QuoteColumn(string columnName) => columnName.ToLowerInvariant();

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetColumnDbType。
        /// 正文中的关键协作调用包括 Unwrap；具体条件和先后次序见方法体。
        /// </summary>
        public string GetColumnDbType(Type type) => DialectShared.Unwrap(type).Name switch
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
        };

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildPrimaryKeyDefinition。
        /// 正文中的关键协作调用包括 ToLowerInvariant、Unwrap；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildPrimaryKeyDefinition(string columnName, Type propertyType)
        {
            var name = columnName.ToLowerInvariant();
            var t = DialectShared.Unwrap(propertyType);
            return (t == typeof(long) || t == typeof(int))
                ? $"{name} BIGSERIAL PRIMARY KEY"
                : $"{name} PRIMARY KEY";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildSplitTablesLikeSql。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildSplitTablesLikeSql(string baseTableName, string dbName) =>
            $"SELECT table_name FROM information_schema.tables WHERE table_name LIKE '{baseTableName.ToLowerInvariant()}_%'";
    }

    /// <summary>MySQL 方言（原 SqlDialectHelper MySQL 分支整体搬迁）。</summary>
    public sealed class MySqlDialect : ISqlDialect
    {
        public ESQLType DbType => ESQLType.MySQL;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildConnectionString。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、IsNullOrEmpty；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildConnectionString(SQLParam param, string? dbNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(param.ConnectionString))
                return param.ConnectionString;
            var db = dbNameOverride ?? param.DBName;
            return string.IsNullOrEmpty(db)
                ? $"Server={param.ServerIP};Port={param.Port};Uid={param.UserName};Pwd={param.Password}"
                : $"Server={param.ServerIP};Port={param.Port};Database={db};Uid={param.UserName};Pwd={param.Password}";
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateConnection。
        /// </summary>
        public IDbConnection CreateConnection(string connectionString) =>
            new MySqlConnector.MySqlConnection(connectionString);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsureDatabase。
        /// 正文中的关键协作调用包括 CreateConnection、BuildConnectionString、Open、Execute、BuildCreateDatabaseSql；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void EnsureDatabase(SQLParam param)
        {
            try
            {
                using var conn = CreateConnection(BuildConnectionString(param));
                conn.Open();
                return;
            }
            catch { }
            using var sys = CreateConnection(BuildConnectionString(param, string.Empty));
            sys.Open();
            sys.Execute(BuildCreateDatabaseSql(param.DBName));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCreateDatabaseSql。
        /// </summary>
        public string BuildCreateDatabaseSql(string dbName) =>
            $"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCheckTableExistsSql。
        /// </summary>
        public string BuildCheckTableExistsSql(string tableName, string dbName) =>
            $"SELECT 1 FROM information_schema.tables WHERE table_schema = '{dbName}' AND table_name = '{tableName}'";
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetCreateTableSuffix。
        /// </summary>
        public string GetCreateTableSuffix() => " ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteTable。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string QuoteTable(string tableName) => $"`{tableName.ToLowerInvariant()}`";
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteColumn。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string QuoteColumn(string columnName) => $"`{columnName.ToLowerInvariant()}`";

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetColumnDbType。
        /// 正文中的关键协作调用包括 Unwrap；具体条件和先后次序见方法体。
        /// </summary>
        public string GetColumnDbType(Type type) => DialectShared.Unwrap(type).Name switch
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
        };

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildPrimaryKeyDefinition。
        /// 正文中的关键协作调用包括 Unwrap、ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildPrimaryKeyDefinition(string columnName, Type propertyType)
        {
            var t = DialectShared.Unwrap(propertyType);
            var name = columnName.ToLowerInvariant();
            return (t == typeof(long) || t == typeof(int))
                ? $"`{name}` BIGINT AUTO_INCREMENT PRIMARY KEY"
                : $"`{name}` PRIMARY KEY";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildSplitTablesLikeSql。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildSplitTablesLikeSql(string baseTableName, string dbName) =>
            $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{dbName}' AND table_name LIKE '{baseTableName.ToLowerInvariant()}_%'";
    }

    /// <summary>
    /// SQLite 方言。无服务器：建库=建目录；文件在首次连接/建表时懒建。
    /// 命名沿用小写约定，与 PG/MySQL 保持一致。
    /// </summary>
    public sealed class SqliteDialect : ISqlDialect
    {
        public ESQLType DbType => ESQLType.SQLite;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildConnectionString。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildConnectionString(SQLParam param, string? dbNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(param.ConnectionString))
                return param.ConnectionString;
            var file = !string.IsNullOrWhiteSpace(dbNameOverride)
                ? dbNameOverride
                : param.EffectiveFilePath;
            return $"Data Source={file}";
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateConnection。
        /// </summary>
        public IDbConnection CreateConnection(string connectionString) =>
            new Microsoft.Data.Sqlite.SqliteConnection(connectionString);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsureDatabase。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、GetDirectoryName、GetFullPath、IsNullOrEmpty、CreateDirectory；具体条件和先后次序见方法体。
        /// </summary>
        public void EnsureDatabase(SQLParam param)
        {
            var file = param.EffectiveFilePath;
            if (string.IsNullOrWhiteSpace(file)) return;
            var dir = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCreateDatabaseSql。
        /// </summary>
        public string BuildCreateDatabaseSql(string dbName) => string.Empty;
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCheckTableExistsSql。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildCheckTableExistsSql(string tableName, string dbName) =>
            $"SELECT 1 FROM sqlite_master WHERE type='table' AND name='{tableName.ToLowerInvariant()}'";
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetCreateTableSuffix。
        /// </summary>
        public string GetCreateTableSuffix() => string.Empty;
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteTable。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string QuoteTable(string tableName) => $"\"{tableName.ToLowerInvariant()}\"";
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteColumn。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string QuoteColumn(string columnName) => $"\"{columnName.ToLowerInvariant()}\"";

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetColumnDbType。
        /// 正文中的关键协作调用包括 Unwrap；具体条件和先后次序见方法体。
        /// </summary>
        public string GetColumnDbType(Type type) => DialectShared.Unwrap(type).Name switch
        {
            "Int32" => "INTEGER",
            "Int64" => "INTEGER",
            "String" => "TEXT",
            "DateTime" => "TEXT",
            "Boolean" => "INTEGER",
            "Double" => "REAL",
            "Single" => "REAL",
            "Decimal" => "NUMERIC",
            "Byte[]" => "BLOB",
            _ => "TEXT"
        };

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildPrimaryKeyDefinition。
        /// 正文中的关键协作调用包括 ToLowerInvariant、Unwrap；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildPrimaryKeyDefinition(string columnName, Type propertyType)
        {
            var name = $"\"{columnName.ToLowerInvariant()}\"";
            var t = DialectShared.Unwrap(propertyType);
            return (t == typeof(long) || t == typeof(int))
                ? $"{name} INTEGER PRIMARY KEY AUTOINCREMENT"
                : $"{name} PRIMARY KEY";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildSplitTablesLikeSql。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildSplitTablesLikeSql(string baseTableName, string dbName) =>
            $"SELECT name FROM sqlite_master WHERE type='table' AND name LIKE '{baseTableName.ToLowerInvariant()}_%'";
    }

    /// <summary>SQL Server 方言。标识符用 [x]，自增用 IDENTITY，建库用中括号转义。</summary>
    public sealed class SqlServerDialect : ISqlDialect
    {
        public ESQLType DbType => ESQLType.SQL_Server;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildConnectionString。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildConnectionString(SQLParam param, string? dbNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(param.ConnectionString))
                return param.ConnectionString;
            var db = dbNameOverride ?? param.DBName;
            var server = string.IsNullOrWhiteSpace(param.Port)
                ? param.ServerIP
                : $"{param.ServerIP},{param.Port}";
            var cs = $"Server={server};Database={db};User Id={param.UserName};Password={param.Password};";
            if (param.TrustServerCertificate)
                cs += "TrustServerCertificate=True;";
            return cs;
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateConnection。
        /// </summary>
        public IDbConnection CreateConnection(string connectionString) =>
            new Microsoft.Data.SqlClient.SqlConnection(connectionString);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsureDatabase。
        /// 正文中的关键协作调用包括 CreateConnection、BuildConnectionString、Open、Execute、BuildCreateDatabaseSql；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void EnsureDatabase(SQLParam param)
        {
            try
            {
                using var conn = CreateConnection(BuildConnectionString(param));
                conn.Open();
                return;
            }
            catch { }
            using var sys = CreateConnection(BuildConnectionString(param, "master"));
            sys.Open();
            sys.Execute(BuildCreateDatabaseSql(param.DBName));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCreateDatabaseSql。
        /// </summary>
        public string BuildCreateDatabaseSql(string dbName) => $"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}]";
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildCheckTableExistsSql。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildCheckTableExistsSql(string tableName, string dbName) =>
            $"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG = '{dbName}' AND TABLE_NAME = '{tableName.ToLowerInvariant()}'";
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetCreateTableSuffix。
        /// </summary>
        public string GetCreateTableSuffix() => string.Empty;
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteTable。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string QuoteTable(string tableName) => $"[{tableName.ToLowerInvariant()}]";
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 QuoteColumn。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string QuoteColumn(string columnName) => $"[{columnName.ToLowerInvariant()}]";

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetColumnDbType。
        /// 正文中的关键协作调用包括 Unwrap；具体条件和先后次序见方法体。
        /// </summary>
        public string GetColumnDbType(Type type) => DialectShared.Unwrap(type).Name switch
        {
            "Int32" => "INT",
            "Int64" => "BIGINT",
            "String" => "NVARCHAR(MAX)",
            "DateTime" => "DATETIME2",
            "Boolean" => "BIT",
            "Double" => "FLOAT",
            "Single" => "REAL",
            "Decimal" => "DECIMAL(18,2)",
            "Byte[]" => "VARBINARY(MAX)",
            _ => "NVARCHAR(MAX)"
        };

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildPrimaryKeyDefinition。
        /// 正文中的关键协作调用包括 ToLowerInvariant、Unwrap；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildPrimaryKeyDefinition(string columnName, Type propertyType)
        {
            var name = $"[{columnName.ToLowerInvariant()}]";
            var t = DialectShared.Unwrap(propertyType);
            return (t == typeof(long) || t == typeof(int))
                ? $"{name} BIGINT IDENTITY(1,1) PRIMARY KEY"
                : $"{name} PRIMARY KEY";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildSplitTablesLikeSql。
        /// 正文中的关键协作调用包括 ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string BuildSplitTablesLikeSql(string baseTableName, string dbName) =>
            $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG = '{dbName}' AND TABLE_NAME LIKE '{baseTableName.ToLowerInvariant()}_%'";
    }
}
