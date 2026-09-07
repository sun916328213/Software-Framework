using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;

namespace DataManager.Tests.SQL
{
    public class SqlDialectHelperTests
    {
        private static SQLParam CreateParam(ESQLType dbType) => new()
        {
            ServerIP = "127.0.0.1",
            Port = "5432",
            UserName = "postgres",
            Password = "secret",
            DBName = "testdb",
            DBType = dbType,
        };

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

        [Fact]
        public void BuildConnectionString_MySQL_WithDatabase()
        {
            var connStr = SqlDialectHelper.BuildConnectionString(CreateParam(ESQLType.MySQL));
            Assert.Contains("Server=127.0.0.1", connStr);
            Assert.Contains("Database=testdb", connStr);
            Assert.Contains("Uid=postgres", connStr);
            Assert.Contains("Pwd=secret", connStr);
        }

        [Fact]
        public void BuildConnectionString_MySQL_WithoutDatabase_OmitsDatabase()
        {
            var connStr = SqlDialectHelper.BuildConnectionString(CreateParam(ESQLType.MySQL), string.Empty);
            Assert.DoesNotContain("Database=", connStr);
        }

        [Fact]
        public void BuildCreateDatabaseSql_PostgreSQL_UsesQuotes()
        {
            var sql = SqlDialectHelper.BuildCreateDatabaseSql("newdb", ESQLType.PostgreSQL);
            Assert.Equal("CREATE DATABASE \"newdb\"", sql);
        }

        [Fact]
        public void BuildCreateDatabaseSql_MySQL_SetsUtf8()
        {
            var sql = SqlDialectHelper.BuildCreateDatabaseSql("newdb", ESQLType.MySQL);
            Assert.Contains("CHARACTER SET utf8mb4", sql);
        }

        [Fact]
        public void BuildCheckTableExistsSql_PostgreSQL_UsesInformationSchema()
        {
            var sql = SqlDialectHelper.BuildCheckTableExistsSql("testentity", "testdb", ESQLType.PostgreSQL);
            Assert.Contains("information_schema.tables", sql);
            Assert.Contains("table_name = 'testentity'", sql);
        }

        [Fact]
        public void BuildCheckTableExistsSql_MySQL_UsesSchemaFilter()
        {
            var sql = SqlDialectHelper.BuildCheckTableExistsSql("TestEntity", "testdb", ESQLType.MySQL);
            Assert.Contains("table_schema = 'testdb'", sql);
        }

        [Fact]
        public void WrapTableName_MySQL_WrapsWithBackticks()
        {
            Assert.Equal("`t1`", SqlDialectHelper.WrapTableName("t1", ESQLType.MySQL));
        }

        [Fact]
        public void GetCreateTableSuffix_MySQL_HasInnoDB()
        {
            Assert.Contains("ENGINE=InnoDB", SqlDialectHelper.GetCreateTableSuffix(ESQLType.MySQL));
            Assert.Equal(string.Empty, SqlDialectHelper.GetCreateTableSuffix(ESQLType.PostgreSQL));
        }

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

        [Fact]
        public void GetDbType_NullableInt_MapsToInteger()
        {
            Assert.Equal("INTEGER", SqlDialectHelper.GetDbType(typeof(int?), ESQLType.PostgreSQL));
        }

        [Fact]
        public void GetDbType_MySQL_Boolean_TinyInt()
        {
            Assert.Equal("TINYINT(1)", SqlDialectHelper.GetDbType(typeof(bool), ESQLType.MySQL));
        }

        [Fact]
        public void BuildPrimaryKeyDefinition_PostgreSQL_LongKey_IsBigSerial()
        {
            var sql = SqlDialectHelper.BuildPrimaryKeyDefinition("Id", typeof(long), ESQLType.PostgreSQL);
            Assert.Equal("id BIGSERIAL PRIMARY KEY", sql);
        }

        [Fact]
        public void BuildPrimaryKeyDefinition_MySQL_IntKey_AutoIncrement()
        {
            var sql = SqlDialectHelper.BuildPrimaryKeyDefinition("Id", typeof(int), ESQLType.MySQL);
            Assert.Contains("AUTO_INCREMENT PRIMARY KEY", sql);
        }

        [Fact]
        public void CreateConnection_PostgreSQL_ReturnsConnection()
        {
            using var conn = SqlDialectHelper.CreateConnection(ESQLType.PostgreSQL, "Host=127.0.0.1");
            Assert.NotNull(conn);
        }

        [Fact]
        public void CreateConnection_Unsupported_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => SqlDialectHelper.CreateConnection(ESQLType.SQLite, "Data Source=test.db"));
        }
    }
}
