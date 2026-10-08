// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlDialectHelperTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// SqlDialectHelperTests 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    public class SqlDialectHelperTests
    {
        /// <summary>
        /// 测试相关入口 CreateParam；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private static SQLParam CreateParam(ESQLType dbType) => new()
        {
            ServerIP = "127.0.0.1",
            Port = "5432",
            UserName = "postgres",
            Password = "secret",
            DBName = "testdb",
            DBType = dbType,
        };

        /// <summary>
        /// 测试相关入口 BuildConnectionString_PostgreSQL_ContainsAllParts；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildConnectionString、CreateParam、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildConnectionString_PostgreSQL_ContainsAllParts()
        {
            var connStr = SqlDialectHelper.BuildConnectionString(CreateParam(ESQLType.PostgreSQL));
            Assert.Contains("Host=127.0.0.1", connStr);
            Assert.Contains("Port=5432", connStr);
            Assert.Contains("Database=testdb", connStr);
            Assert.Contains("Username=postgres", connStr);
            Assert.Contains("Password=secret", connStr);
        }

        /// <summary>
        /// 测试相关入口 BuildConnectionString_MySQL_WithDatabase；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildConnectionString、CreateParam、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildConnectionString_MySQL_WithDatabase()
        {
            var connStr = SqlDialectHelper.BuildConnectionString(CreateParam(ESQLType.MySQL));
            Assert.Contains("Server=127.0.0.1", connStr);
            Assert.Contains("Database=testdb", connStr);
            Assert.Contains("Uid=postgres", connStr);
            Assert.Contains("Pwd=secret", connStr);
        }

        /// <summary>
        /// 测试相关入口 BuildConnectionString_MySQL_WithoutDatabase_OmitsDatabase；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildConnectionString、CreateParam、DoesNotContain；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildConnectionString_MySQL_WithoutDatabase_OmitsDatabase()
        {
            var connStr = SqlDialectHelper.BuildConnectionString(CreateParam(ESQLType.MySQL), string.Empty);
            Assert.DoesNotContain("Database=", connStr);
        }

        /// <summary>
        /// 测试相关入口 BuildCreateDatabaseSql_PostgreSQL_UsesQuotes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildCreateDatabaseSql、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildCreateDatabaseSql_PostgreSQL_UsesQuotes()
        {
            var sql = SqlDialectHelper.BuildCreateDatabaseSql("newdb", ESQLType.PostgreSQL);
            Assert.Equal("CREATE DATABASE \"newdb\"", sql);
        }

        /// <summary>
        /// 测试相关入口 BuildCreateDatabaseSql_MySQL_SetsUtf8；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildCreateDatabaseSql、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildCreateDatabaseSql_MySQL_SetsUtf8()
        {
            var sql = SqlDialectHelper.BuildCreateDatabaseSql("newdb", ESQLType.MySQL);
            Assert.Contains("CHARACTER SET utf8mb4", sql);
        }

        /// <summary>
        /// 测试相关入口 BuildCheckTableExistsSql_PostgreSQL_UsesInformationSchema；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildCheckTableExistsSql、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildCheckTableExistsSql_PostgreSQL_UsesInformationSchema()
        {
            var sql = SqlDialectHelper.BuildCheckTableExistsSql("testentity", "testdb", ESQLType.PostgreSQL);
            Assert.Contains("information_schema.tables", sql);
            Assert.Contains("table_name = 'testentity'", sql);
        }

        /// <summary>
        /// 测试相关入口 BuildCheckTableExistsSql_MySQL_UsesSchemaFilter；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildCheckTableExistsSql、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildCheckTableExistsSql_MySQL_UsesSchemaFilter()
        {
            var sql = SqlDialectHelper.BuildCheckTableExistsSql("TestEntity", "testdb", ESQLType.MySQL);
            Assert.Contains("table_schema = 'testdb'", sql);
        }

        /// <summary>
        /// 测试相关入口 WrapTableName_MySQL_WrapsWithBackticks；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、WrapTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void WrapTableName_MySQL_WrapsWithBackticks()
        {
            Assert.Equal("`t1`", SqlDialectHelper.WrapTableName("t1", ESQLType.MySQL));
        }

        /// <summary>
        /// 测试相关入口 GetCreateTableSuffix_MySQL_HasInnoDB；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Contains、GetCreateTableSuffix、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetCreateTableSuffix_MySQL_HasInnoDB()
        {
            Assert.Contains("ENGINE=InnoDB", SqlDialectHelper.GetCreateTableSuffix(ESQLType.MySQL));
            Assert.Equal(string.Empty, SqlDialectHelper.GetCreateTableSuffix(ESQLType.PostgreSQL));
        }

        /// <summary>
        /// 测试相关入口 GetDbType_PostgreSQL_MapsTypes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDbType；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData("Int32", "INTEGER")]
        [InlineData("Int64", "BIGINT")]
        [InlineData("String", "TEXT")]
        [InlineData("DateTime", "TIMESTAMP WITHOUT TIME ZONE")]
        [InlineData("Boolean", "BOOLEAN")]
        public void GetDbType_PostgreSQL_MapsTypes(string typeName, string expected)
        {
            var type = typeName switch
            {
                "Int32" => typeof(int),
                "Int64" => typeof(long),
                "String" => typeof(string),
                "DateTime" => typeof(DateTime),
                "Boolean" => typeof(bool),
                _ => typeof(object),
            };
            Assert.Equal(expected, SqlDialectHelper.GetDbType(type, ESQLType.PostgreSQL));
        }

        /// <summary>
        /// 测试相关入口 GetDbType_NullableInt_MapsToInteger；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDbType；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDbType_NullableInt_MapsToInteger()
        {
            Assert.Equal("INTEGER", SqlDialectHelper.GetDbType(typeof(int?), ESQLType.PostgreSQL));
        }

        /// <summary>
        /// 测试相关入口 GetDbType_MySQL_Boolean_TinyInt；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDbType；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDbType_MySQL_Boolean_TinyInt()
        {
            Assert.Equal("TINYINT(1)", SqlDialectHelper.GetDbType(typeof(bool), ESQLType.MySQL));
        }

        /// <summary>
        /// 测试相关入口 BuildPrimaryKeyDefinition_PostgreSQL_LongKey_IsBigSerial；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildPrimaryKeyDefinition、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildPrimaryKeyDefinition_PostgreSQL_LongKey_IsBigSerial()
        {
            var sql = SqlDialectHelper.BuildPrimaryKeyDefinition("Id", typeof(long), ESQLType.PostgreSQL);
            Assert.Equal("id BIGSERIAL PRIMARY KEY", sql);
        }

        /// <summary>
        /// 测试相关入口 BuildPrimaryKeyDefinition_MySQL_IntKey_AutoIncrement；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildPrimaryKeyDefinition、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildPrimaryKeyDefinition_MySQL_IntKey_AutoIncrement()
        {
            var sql = SqlDialectHelper.BuildPrimaryKeyDefinition("Id", typeof(int), ESQLType.MySQL);
            Assert.Contains("AUTO_INCREMENT PRIMARY KEY", sql);
        }

        /// <summary>
        /// 测试相关入口 CreateConnection_PostgreSQL_ReturnsConnection；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateConnection、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CreateConnection_PostgreSQL_ReturnsConnection()
        {
            using var conn = SqlDialectHelper.CreateConnection(ESQLType.PostgreSQL, "Host=127.0.0.1");
            Assert.NotNull(conn);
        }

        /// <summary>
        /// 测试相关入口 CreateConnection_Unsupported_Throws；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、CreateConnection；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CreateConnection_Unsupported_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => SqlDialectHelper.CreateConnection((ESQLType)999, "Data Source=test.db"));
        }

        /// <summary>
        /// 测试相关入口 CreateConnection_Sqlite_ReturnsConnection；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateConnection、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CreateConnection_Sqlite_ReturnsConnection()
        {
            using var conn = SqlDialectHelper.CreateConnection(ESQLType.SQLite, "Data Source=:memory:");
            Assert.NotNull(conn);
        }

        /// <summary>
        /// 测试相关入口 CreateConnection_SqlServer_ReturnsConnection；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateConnection、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CreateConnection_SqlServer_ReturnsConnection()
        {
            using var conn = SqlDialectHelper.CreateConnection(ESQLType.SQL_Server, "Server=localhost;Database=master;User Id=sa;Password=pwd;TrustServerCertificate=True;");
            Assert.NotNull(conn);
        }

        /// <summary>
        /// 测试相关入口 BuildConnectionString_Sqlite_UsesFilePath；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildConnectionString、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildConnectionString_Sqlite_UsesFilePath()
        {
            var param = new SQLParam { Name = "lite", FilePath = "workbench.db", DBType = ESQLType.SQLite };
            var connStr = SqlDialectHelper.BuildConnectionString(param);
            Assert.Contains("Data Source=workbench.db", connStr);
        }

        /// <summary>
        /// 测试相关入口 BuildConnectionString_SqlServer_TrustsCertByDefault；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BuildConnectionString、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildConnectionString_SqlServer_TrustsCertByDefault()
        {
            var param = new SQLParam
            {
                Name = "ms", ServerIP = "localhost", DBName = "workbench",
                UserName = "sa", Password = "pwd", DBType = ESQLType.SQL_Server,
            };
            var connStr = SqlDialectHelper.BuildConnectionString(param);
            Assert.Contains("TrustServerCertificate=True", connStr);
        }

        /// <summary>
        /// 测试相关入口 Validate_Sqlite_RequiresFilePath；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Validate_Sqlite_RequiresFilePath()
        {
            var param = new SQLParam { Name = "lite", DBType = ESQLType.SQLite };
            Assert.Throws<ArgumentException>(() => param.Validate());
            param.FilePath = "workbench.db";
            param.Validate();
        }

        /// <summary>
        /// 测试相关入口 WrapTableName_SqlServer_UsesBrackets；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、WrapTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void WrapTableName_SqlServer_UsesBrackets()
        {
            Assert.Equal("[t1]", SqlDialectHelper.WrapTableName("T1", ESQLType.SQL_Server));
        }

        /// <summary>
        /// 测试相关入口 SqlServer_Dialect_GeneratesExpectedSql；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、BuildCreateDatabaseSql、Contains、BuildCheckTableExistsSql、GetDbType；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlServer_Dialect_GeneratesExpectedSql()
        {
            // 建库幂等、无 IF NOT EXISTS（SQL Server 不支持该语法，由调用方先判存）。
            Assert.Equal(
                "IF DB_ID(N'mydb') IS NULL CREATE DATABASE [mydb]",
                SqlDialectHelper.BuildCreateDatabaseSql("mydb", ESQLType.SQL_Server));
            Assert.Contains("INFORMATION_SCHEMA.TABLES",
                SqlDialectHelper.BuildCheckTableExistsSql("t1", "mydb", ESQLType.SQL_Server));
            Assert.Equal("INT", SqlDialectHelper.GetDbType(typeof(int), ESQLType.SQL_Server));
            Assert.Equal("DATETIME2", SqlDialectHelper.GetDbType(typeof(DateTime), ESQLType.SQL_Server));
            var pk = SqlDialectHelper.BuildPrimaryKeyDefinition("Id", typeof(long), ESQLType.SQL_Server);
            Assert.Contains("IDENTITY(1,1) PRIMARY KEY", pk);
        }

        /// <summary>
        /// 测试相关入口 Sqlite_Dialect_GeneratesExpectedSql；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDbType、BuildPrimaryKeyDefinition、Contains、BuildCheckTableExistsSql；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Sqlite_Dialect_GeneratesExpectedSql()
        {
            Assert.Equal("INTEGER", SqlDialectHelper.GetDbType(typeof(int), ESQLType.SQLite));
            Assert.Equal("TEXT", SqlDialectHelper.GetDbType(typeof(DateTime), ESQLType.SQLite));
            var pk = SqlDialectHelper.BuildPrimaryKeyDefinition("Id", typeof(long), ESQLType.SQLite);
            Assert.Contains("AUTOINCREMENT", pk);
            Assert.Contains("sqlite_master",
                SqlDialectHelper.BuildCheckTableExistsSql("t1", "f.db", ESQLType.SQLite));
        }

        /// <summary>
        /// 测试相关入口 Validate_ConnectionStringPassthrough_SkipsServerFields；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Validate；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Validate_ConnectionStringPassthrough_SkipsServerFields()
        {
            // P3-1 直通串：SQL Server 无需拆字段即可过校验。
            var param = new SQLParam
            {
                Name = "ms",
                DBType = ESQLType.SQL_Server,
                ConnectionString = "Server=localhost;Database=m;User Id=sa;Password=p;TrustServerCertificate=True;",
            };
            param.Validate();
        }
    }
}
