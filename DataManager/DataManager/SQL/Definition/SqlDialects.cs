using Dapper;
using DataManager.SQL.Definition;
using System.Data;

namespace DataManager.SQL.Definition
{
    internal static class DialectShared
    {
        internal static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

        internal static string QuoteIdent(string name, string open, string close) => $"{open}{name}{close}";
    }

    /// <summary>PostgreSQL 方言（原 SqlDialectHelper PG 分支整体搬迁）。</summary>
    public sealed class PostgresDialect : ISqlDialect
    {
        public ESQLType DbType => ESQLType.PostgreSQL;

        public string BuildConnectionString(SQLParam param, string? dbNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(param.ConnectionString))
                return param.ConnectionString;
            var db = dbNameOverride ?? param.DBName;
            return $"Host={param.ServerIP};Port={param.Port};Database={db};Username={param.UserName};Password={param.Password}";
        }

        public IDbConnection CreateConnection(string connectionString) =>
            new Npgsql.NpgsqlConnection(connectionString);

        public void EnsureDatabase(SQLParam param)
        {
            if (IsDatabaseExists(param)) return;
            using var conn = CreateConnection(BuildConnectionString(param, "postgres"));
            conn.Open();
            conn.Execute(BuildCreateDatabaseSql(param.DBName));
        }

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

        public string BuildCreateDatabaseSql(string dbName) => $"CREATE DATABASE \"{dbName}\"";
        public string BuildCheckTableExistsSql(string tableName, string dbName) =>
            $"SELECT 1 FROM information_schema.tables WHERE table_name = '{tableName}'";
        public string GetCreateTableSuffix() => string.Empty;
        public string QuoteTable(string tableName) => tableName.ToLowerInvariant();
        public string QuoteColumn(string columnName) => columnName.ToLowerInvariant();

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

        public string BuildPrimaryKeyDefinition(string columnName, Type propertyType)
        {
            var name = columnName.ToLowerInvariant();
            var t = DialectShared.Unwrap(propertyType);
            return (t == typeof(long) || t == typeof(int))
                ? $"{name} BIGSERIAL PRIMARY KEY"
                : $"{name} PRIMARY KEY";
        }

        public string BuildSplitTablesLikeSql(string baseTableName, string dbName) =>
            $"SELECT table_name FROM information_schema.tables WHERE table_name LIKE '{baseTableName.ToLowerInvariant()}_%'";
    }

    /// <summary>MySQL 方言（原 SqlDialectHelper MySQL 分支整体搬迁）。</summary>
    public sealed class MySqlDialect : ISqlDialect
    {
        public ESQLType DbType => ESQLType.MySQL;

        public string BuildConnectionString(SQLParam param, string? dbNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(param.ConnectionString))
                return param.ConnectionString;
            var db = dbNameOverride ?? param.DBName;
            return string.IsNullOrEmpty(db)
                ? $"Server={param.ServerIP};Port={param.Port};Uid={param.UserName};Pwd={param.Password}"
                : $"Server={param.ServerIP};Port={param.Port};Database={db};Uid={param.UserName};Pwd={param.Password}";
        }

        public IDbConnection CreateConnection(string connectionString) =>
            new MySqlConnector.MySqlConnection(connectionString);

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

        public string BuildCreateDatabaseSql(string dbName) =>
            $"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
        public string BuildCheckTableExistsSql(string tableName, string dbName) =>
            $"SELECT 1 FROM information_schema.tables WHERE table_schema = '{dbName}' AND table_name = '{tableName}'";
        public string GetCreateTableSuffix() => " ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        public string QuoteTable(string tableName) => $"`{tableName.ToLowerInvariant()}`";
        public string QuoteColumn(string columnName) => $"`{columnName.ToLowerInvariant()}`";

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

        public string BuildPrimaryKeyDefinition(string columnName, Type propertyType)
        {
            var t = DialectShared.Unwrap(propertyType);
            var name = columnName.ToLowerInvariant();
            return (t == typeof(long) || t == typeof(int))
                ? $"`{name}` BIGINT AUTO_INCREMENT PRIMARY KEY"
                : $"`{name}` PRIMARY KEY";
        }

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

        public string BuildConnectionString(SQLParam param, string? dbNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(param.ConnectionString))
                return param.ConnectionString;
            var file = !string.IsNullOrWhiteSpace(dbNameOverride)
                ? dbNameOverride
                : param.EffectiveFilePath;
            return $"Data Source={file}";
        }

        public IDbConnection CreateConnection(string connectionString) =>
            new Microsoft.Data.Sqlite.SqliteConnection(connectionString);

        public void EnsureDatabase(SQLParam param)
        {
            var file = param.EffectiveFilePath;
            if (string.IsNullOrWhiteSpace(file)) return;
            var dir = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        public string BuildCreateDatabaseSql(string dbName) => string.Empty;
        public string BuildCheckTableExistsSql(string tableName, string dbName) =>
            $"SELECT 1 FROM sqlite_master WHERE type='table' AND name='{tableName.ToLowerInvariant()}'";
        public string GetCreateTableSuffix() => string.Empty;
        public string QuoteTable(string tableName) => $"\"{tableName.ToLowerInvariant()}\"";
        public string QuoteColumn(string columnName) => $"\"{columnName.ToLowerInvariant()}\"";

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

        public string BuildPrimaryKeyDefinition(string columnName, Type propertyType)
        {
            var name = $"\"{columnName.ToLowerInvariant()}\"";
            var t = DialectShared.Unwrap(propertyType);
            return (t == typeof(long) || t == typeof(int))
                ? $"{name} INTEGER PRIMARY KEY AUTOINCREMENT"
                : $"{name} PRIMARY KEY";
        }

        public string BuildSplitTablesLikeSql(string baseTableName, string dbName) =>
            $"SELECT name FROM sqlite_master WHERE type='table' AND name LIKE '{baseTableName.ToLowerInvariant()}_%'";
    }

    /// <summary>SQL Server 方言。标识符用 [x]，自增用 IDENTITY，建库用中括号转义。</summary>
    public sealed class SqlServerDialect : ISqlDialect
    {
        public ESQLType DbType => ESQLType.SQL_Server;

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

        public IDbConnection CreateConnection(string connectionString) =>
            new Microsoft.Data.SqlClient.SqlConnection(connectionString);

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

        public string BuildCreateDatabaseSql(string dbName) => $"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}]";
        public string BuildCheckTableExistsSql(string tableName, string dbName) =>
            $"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG = '{dbName}' AND TABLE_NAME = '{tableName.ToLowerInvariant()}'";
        public string GetCreateTableSuffix() => string.Empty;
        public string QuoteTable(string tableName) => $"[{tableName.ToLowerInvariant()}]";
        public string QuoteColumn(string columnName) => $"[{columnName.ToLowerInvariant()}]";

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

        public string BuildPrimaryKeyDefinition(string columnName, Type propertyType)
        {
            var name = $"[{columnName.ToLowerInvariant()}]";
            var t = DialectShared.Unwrap(propertyType);
            return (t == typeof(long) || t == typeof(int))
                ? $"{name} BIGINT IDENTITY(1,1) PRIMARY KEY"
                : $"{name} PRIMARY KEY";
        }

        public string BuildSplitTablesLikeSql(string baseTableName, string dbName) =>
            $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG = '{dbName}' AND TABLE_NAME LIKE '{baseTableName.ToLowerInvariant()}_%'";
    }
}
