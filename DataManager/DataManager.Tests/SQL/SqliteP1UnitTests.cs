using DataManager.SQL;
using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DapperSplitType = DataManager.SQL.Dapper.SplitType;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P1 纯单测：G-7 建库 SQL / G-8 Validate 矩阵 / G-9 DapperSqlBuilder /
    /// G-10 DapperSplitRouter 守卫 / G-11 DapperNaming / G-12 引号方言 / G-16 SQLManager。
    /// </summary>
    public class SqliteP1UnitTests
    {
        #region G-7 BuildCheckDatabaseExistsSql / BuildCreateDatabaseSql

        [Theory]
        [InlineData(ESQLType.PostgreSQL, "pg_database")]
        [InlineData(ESQLType.MySQL, "INFORMATION_SCHEMA.SCHEMATA")]
        [InlineData(ESQLType.SQL_Server, "sys.databases")]
        public void BuildCheckDatabaseExistsSql_KnownTypes_ContainsCatalog(ESQLType dbType, string catalog)
        {
            var sql = SqlDialectHelper.BuildCheckDatabaseExistsSql("targetdb", dbType);
            Assert.Contains(catalog, sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("targetdb", sql);
        }

        [Fact]
        public void BuildCheckDatabaseExistsSql_Sqlite_IsSelectOne()
        {
            Assert.Equal("SELECT 1", SqlDialectHelper.BuildCheckDatabaseExistsSql("x", ESQLType.SQLite));
        }

        [Fact]
        public void BuildCheckDatabaseExistsSql_Unsupported_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => SqlDialectHelper.BuildCheckDatabaseExistsSql("x", (ESQLType)999));
        }

        [Theory]
        [InlineData(ESQLType.PostgreSQL, "CREATE DATABASE")]
        [InlineData(ESQLType.MySQL, "CREATE DATABASE")]
        [InlineData(ESQLType.SQL_Server, "CREATE DATABASE")]
        public void BuildCreateDatabaseSql_KnownServerTypes_ContainsCreate(ESQLType dbType, string keyword)
        {
            var sql = SqlDialectHelper.BuildCreateDatabaseSql("newdb", dbType);
            Assert.Contains(keyword, sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("newdb", sql);
        }

        [Fact]
        public void BuildCreateDatabaseSql_Sqlite_Unsupported_OrNoCreate()
        {
            // SQLite 无建库语句；方言要么抛要么返回空——锁定不产生错误 CREATE DATABASE。
            try
            {
                var sql = SqlDialectHelper.BuildCreateDatabaseSql("f.db", ESQLType.SQLite);
                Assert.DoesNotContain("CREATE DATABASE", sql, StringComparison.OrdinalIgnoreCase);
            }
            catch (NotSupportedException)
            {
                // 可接受：方言显式拒绝
            }
        }

        #endregion

        #region G-8 SQLParam.Validate 分支矩阵

        [Fact]
        public void Validate_EmptyName_Throws()
        {
            var p = new SQLParam { Name = " ", DBType = ESQLType.PostgreSQL };
            Assert.Throws<ArgumentException>(() => p.Validate());
        }

        [Fact]
        public void Validate_ConnectionStringPassthrough_OnlyNeedsName()
        {
            var p = new SQLParam { Name = "n", ConnectionString = "Host=x", DBType = ESQLType.MySQL };
            p.Validate();
        }

        [Fact]
        public void Validate_Sqlite_FilePathOrDbName_OK()
        {
            new SQLParam { Name = "n", DBType = ESQLType.SQLite, FilePath = "a.db" }.Validate();
            new SQLParam { Name = "n", DBType = ESQLType.SQLite, DBName = "b.db" }.Validate();
            Assert.Throws<ArgumentException>(() =>
                new SQLParam { Name = "n", DBType = ESQLType.SQLite }.Validate());
        }

        [Fact]
        public void Validate_MissingServerIP_Throws()
        {
            var p = new SQLParam
            {
                Name = "n", DBType = ESQLType.PostgreSQL,
                Port = "5432", DBName = "d", UserName = "u", Password = "p",
            };
            Assert.Throws<ArgumentException>(() => p.Validate());
        }

        [Fact]
        public void Validate_PostgresMissingPort_Throws()
        {
            var p = new SQLParam
            {
                Name = "n", DBType = ESQLType.PostgreSQL, ServerIP = "h",
                DBName = "d", UserName = "u", Password = "p",
            };
            Assert.Throws<ArgumentException>(() => p.Validate());
        }

        [Fact]
        public void Validate_SqlServer_PortOptional()
        {
            // SQL Server 端口可空（命名实例/默认1433）。
            new SQLParam
            {
                Name = "n", DBType = ESQLType.SQL_Server, ServerIP = "h",
                DBName = "d", UserName = "u", Password = "p",
            }.Validate();
        }

        [Fact]
        public void Validate_MySQL_MissingDbName_Throws()
        {
            var p = new SQLParam
            {
                Name = "n", DBType = ESQLType.MySQL, ServerIP = "h", Port = "3306",
                UserName = "u", Password = "p",
            };
            Assert.Throws<ArgumentException>(() => p.Validate());
        }

        [Fact]
        public void Validate_MySQL_MissingUserName_Throws()
        {
            var p = new SQLParam
            {
                Name = "n", DBType = ESQLType.MySQL, ServerIP = "h", Port = "3306",
                DBName = "d", Password = "p",
            };
            Assert.Throws<ArgumentException>(() => p.Validate());
        }

        [Fact]
        public void Validate_NullPassword_Throws_EmptyPassword_OK()
        {
            var bad = new SQLParam
            {
                Name = "n", DBType = ESQLType.PostgreSQL, ServerIP = "h", Port = "5432",
                DBName = "d", UserName = "u",
            };
            bad.Password = null!;
            Assert.Throws<ArgumentException>(() => bad.Validate());

            var ok = new SQLParam
            {
                Name = "n", DBType = ESQLType.PostgreSQL, ServerIP = "h", Port = "5432",
                DBName = "d", UserName = "u", Password = string.Empty,
            };
            ok.Validate();
        }

        [Fact]
        public void Validate_Complete_Postgres_OK()
        {
            new SQLParam
            {
                Name = "n", DBType = ESQLType.PostgreSQL, ServerIP = "h", Port = "5432",
                DBName = "d", UserName = "u", Password = "p",
            }.Validate();
        }

        [Fact]
        public void EffectiveFilePath_PrefersFilePath_OverDbName()
        {
            var p = new SQLParam { FilePath = "prefer.db", DBName = "fallback.db" };
            Assert.Equal("prefer.db", p.EffectiveFilePath);
            p.FilePath = string.Empty;
            Assert.Equal("fallback.db", p.EffectiveFilePath);
        }

        #endregion

        #region G-9 DapperSqlBuilder

        public class BuilderEntity
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public DateTime? Birth { get; set; }
        }

        [Table("ExplicitTable")]
        public class BuilderTableAttr
        {
            [Key]
            public int Id { get; set; }
            [Column("RenamedCol")]
            public string Value { get; set; } = string.Empty;
        }

        private static SQLParam LiteParam(ESQLType dbType = ESQLType.SQLite) => new()
        {
            Name = "b", DBType = dbType, FilePath = "x.db", DBName = "x.db",
        };

        [Fact]
        public void DapperSqlBuilder_GenerateCreateTableSql_Sqlite_UsesIfNotExistsAndQuotes()
        {
            var sql = DapperSqlBuilder<BuilderEntity>.GenerateCreateTableSql(LiteParam(), "builderentity");
            Assert.StartsWith("CREATE TABLE IF NOT EXISTS", sql);
            Assert.Contains("\"builderentity\"", sql);
            Assert.Contains("PRIMARY KEY", sql);
            Assert.Contains("NOT NULL", sql);
            Assert.Contains("\"name\"", sql); // 列名统一小写
            Assert.Contains("\"age\"", sql);
        }

        [Fact]
        public void DapperSqlBuilder_GenerateCreateTableSql_SqlServer_NoIfNotExists()
        {
            var p = LiteParam(ESQLType.SQL_Server);
            var sql = DapperSqlBuilder<BuilderEntity>.GenerateCreateTableSql(p, "builderentity");
            Assert.DoesNotContain("IF NOT EXISTS", sql);
            Assert.StartsWith("CREATE TABLE", sql);
            Assert.Contains("[builderentity]", sql);
        }

        [Fact]
        public void DapperSqlBuilder_BuildSelectAndDeleteSql_QuoteByDialect()
        {
            var lite = LiteParam(ESQLType.SQLite);
            Assert.Equal("SELECT * FROM \"t\"", DapperSqlBuilder<BuilderEntity>.BuildSelectAllSql(lite, "t"));
            Assert.Equal("DELETE FROM \"t\"", DapperSqlBuilder<BuilderEntity>.BuildDeleteAllSql(lite, "t"));

            var ms = LiteParam(ESQLType.SQL_Server);
            Assert.Equal("SELECT * FROM [t]", DapperSqlBuilder<BuilderEntity>.BuildSelectAllSql(ms, "t"));
            Assert.Equal("DELETE FROM [t]", DapperSqlBuilder<BuilderEntity>.BuildDeleteAllSql(ms, "t"));
        }

        [Fact]
        public void DapperSqlBuilder_BuildInsertSql_ListsColumnsAndParameters()
        {
            var props = DapperSqlBuilder<BuilderEntity>.EntityProperties();
            var sql = DapperSqlBuilder<BuilderEntity>.BuildInsertSql(LiteParam(), "t", props);
            Assert.Contains("INSERT INTO \"t\"", sql);
            Assert.Contains("@Id", sql);
            Assert.Contains("@Name", sql);
            Assert.Contains("VALUES", sql);
        }

        [Fact]
        public void DapperSqlBuilder_FindKeyProperty_And_IsDefaultKeyValue()
        {
            var key = DapperSqlBuilder<BuilderEntity>.FindKeyProperty();
            Assert.NotNull(key);
            Assert.Equal("Id", key!.Name);

            var data = new BuilderEntity { Id = 0, Name = "x" };
            Assert.True(DapperSqlBuilder<BuilderEntity>.IsDefaultKeyValue(key!, data));
            data.Id = 5;
            Assert.False(DapperSqlBuilder<BuilderEntity>.IsDefaultKeyValue(key!, data));
        }

        [Fact]
        public void DapperSqlBuilder_BuildUpdateByKeySql_SetsAndWhere()
        {
            var props = DapperSqlBuilder<BuilderEntity>.EntityProperties();
            var key = DapperSqlBuilder<BuilderEntity>.FindKeyProperty()!;
            var others = props.Where(p => p != key);
            var sql = DapperSqlBuilder<BuilderEntity>.BuildUpdateByKeySql(LiteParam(), "t", key, others);
            Assert.Contains("UPDATE \"t\" SET", sql);
            Assert.Contains("WHERE", sql);
            Assert.Contains("id = @Id", sql); // 列名小写，参数名保留原属性名
        }

        [Fact]
        public void DapperSqlBuilder_IsNullableType_Matrix()
        {
            Assert.True(DapperSqlBuilder<BuilderEntity>.IsNullableType(typeof(string)));
            Assert.True(DapperSqlBuilder<BuilderEntity>.IsNullableType(typeof(int?)));
            Assert.False(DapperSqlBuilder<BuilderEntity>.IsNullableType(typeof(int)));
            Assert.False(DapperSqlBuilder<BuilderEntity>.IsNullableType(typeof(DateTime)));
        }

        #endregion

        #region G-10 DapperSplitRouter 守卫

        public class NoKeyEntity
        {
            public string Name { get; set; } = string.Empty;
        }

        [DapperSplitTable(DapperSplitType.Day)]
        public class DapperMarkedSplit
        {
            [Key]
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
        }

        [DbSplitTable(SplitPeriod.Day)]
        public class SharedMarkedSplit
        {
            [Key]
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
        }

        public class PlainEntity
        {
            [Key]
            public long Id { get; set; }
        }

        private static SQLParam RouterParam() => new()
        {
            Name = "r", DBType = ESQLType.SQLite, FilePath = "x.db", DBName = "x.db",
        };

        [Fact]
        public void DapperSplitRouter_IsSplitTableEntity_BothAttributes()
        {
            Assert.True(DapperSplitRouter<DapperMarkedSplit>.IsSplitTableEntity());
            Assert.True(DapperSplitRouter<SharedMarkedSplit>.IsSplitTableEntity());
            Assert.False(DapperSplitRouter<PlainEntity>.IsSplitTableEntity());
        }

        [Fact]
        public void DapperSplitRouter_GetSplitTableName_LegacyAndShared()
        {
            var date = new DateTime(2026, 9, 23);
            var legacy = new DapperSplitRouter<DapperMarkedSplit>(RouterParam());
            Assert.Equal("DapperMarkedSplit_20260923", legacy.GetSplitTableName(date));

            var shared = new DapperSplitRouter<SharedMarkedSplit>(RouterParam());
            Assert.Equal("SharedMarkedSplit_20260923", shared.GetSplitTableName(date));
        }

        [Fact]
        public void DapperSplitRouter_NonSplit_GetSplitTableName_ReturnsTypeName()
        {
            var r = new DapperSplitRouter<PlainEntity>(RouterParam());
            Assert.Equal("PlainEntity", r.GetSplitTableName(DateTime.Today));
        }

        [Fact]
        public void DapperSplitRouter_TxPath_WithoutTx_Ok()
        {
            var r = new DapperSplitRouter<PlainEntity>(RouterParam());
            Assert.Null(r.Tx);
        }

        [Fact]
        public void DapperSplitRouter_NoKeyEntity_FindKeyProperty_IsNull()
        {
            // 无 [Key] 时 FindKeyProperty 返回 null；EnsureTable/插入路径应据此抛错。
            Assert.Null(DapperSqlBuilder<NoKeyEntity>.FindKeyProperty());
        }

        #endregion

        #region G-11 DapperNaming

        [Table("ExplicitTableName")]
        public class NamingSample
        {
            public long Id { get; set; }
            [Column("ExplicitColumn")]
            public string Name { get; set; } = string.Empty;
            public int PlainCol { get; set; }
        }

        [Fact]
        public void LowerTableNameResolver_Lowercases_WithOrWithoutAttribute()
        {
            var resolver = new LowerTableNameResolver();
            Assert.Equal("explicittablename", resolver.ResolveTableName(typeof(NamingSample)));
            Assert.Equal("plainentity", resolver.ResolveTableName(typeof(PlainEntity)));
        }

        [Fact]
        public void LowerColumnNameResolver_Lowercases_WithOrWithoutAttribute()
        {
            var resolver = new LowerColumnNameResolver();
            var withAttr = typeof(NamingSample).GetProperty(nameof(NamingSample.Name))!;
            var plain = typeof(NamingSample).GetProperty(nameof(NamingSample.PlainCol))!;
            Assert.Equal("explicitcolumn", resolver.ResolveColumnName(withAttr));
            Assert.Equal("plaincol", resolver.ResolveColumnName(plain));
        }

        [Fact]
        public void DapperDommelSetup_Ensure_IsIdempotent()
        {
            DapperDommelSetup.Ensure();
            DapperDommelSetup.Ensure(); // 不抛即通过
        }

        #endregion

        #region G-12 引号方言

        [Fact]
        public void QuoteTable_Sqlite_DoubleQuotes_Lowercased()
        {
            Assert.Equal("\"mytable\"", SqlDialectHelper.WrapTableName("MyTable", ESQLType.SQLite));
        }

        [Fact]
        public void QuoteTable_SqlServer_Brackets_Lowercased()
        {
            Assert.Equal("[mytable]", SqlDialectHelper.WrapTableName("MyTable", ESQLType.SQL_Server));
        }

        [Fact]
        public void QuoteTable_MySQL_Backticks_Lowercased()
        {
            Assert.Equal("`mytable`", SqlDialectHelper.WrapTableName("MyTable", ESQLType.MySQL));
        }

        [Fact]
        public void QuoteTable_Postgres_NoQuotes_Lowercased()
        {
            // PG 方言 QuoteTable 仅小写、不加引号（标识符默认合规时无需引号）。
            Assert.Equal("mytable", SqlDialectHelper.WrapTableName("MyTable", ESQLType.PostgreSQL));
        }

        [Fact]
        public void FormatTableName_And_ColumnList_PerDialect()
        {
            Assert.Equal("\"t\"", SqlNamingHelper.FormatTableName("t", ESQLType.SQLite));
            Assert.Equal("[t]", SqlNamingHelper.FormatTableName("t", ESQLType.SQL_Server));
            Assert.Equal(
                "\"a\", \"b\"",
                SqlNamingHelper.FormatColumnList(["A", "B"], ESQLType.SQLite));
            Assert.Equal(
                "[a], [b]",
                SqlNamingHelper.FormatColumnList(["A", "B"], ESQLType.SQL_Server));
            Assert.Equal(string.Empty, SqlNamingHelper.FormatColumnList([], ESQLType.SQLite));
        }

        [Fact]
        public void ConvertTableAndColumn_AlwaysLowerInvariant()
        {
            Assert.Equal("abc", SqlNamingHelper.ConvertTableName("AbC", ESQLType.PostgreSQL));
            Assert.Equal("abc", SqlNamingHelper.ConvertColumnName("AbC", ESQLType.MySQL));
            Assert.True(SqlNamingHelper.NeedsConversion(ESQLType.SQLite));
        }

        #endregion

        #region G-16 SQLManager.Config / 未知工具

        public class CfgEntity
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private static SQLParam NamedParam(string name, ESQLToolType tool) => new()
        {
            Name = name,
            ServerIP = "127.0.0.1",
            Port = "5432",
            UserName = "postgres",
            Password = "postgres",
            DBName = "workbench_test",
            DBType = ESQLType.PostgreSQL,
            ToolType = tool,
        };

        [Fact]
        public void Config_List_ThrowsNotSupported_UntilTypedPathExists()
        {
            // 现状：SQLManager.Config 调非泛型 Create(SQLParam)，后者恒抛
            // NotSupportedException（需要实体类型 T 才能建实例）。
            // 锁定该契约，避免静默半初始化。
            var manager = new SQLManager();
            Assert.Throws<NotSupportedException>(() => manager.Config(new List<SQLParam>
            {
                NamedParam("cfg-a", ESQLToolType.Dapper),
            }));
            Assert.Equal(0, manager.Count);
        }

        [Fact]
        public void Create_UnknownToolType_ThrowsNotSupported()
        {
            var manager = new SQLManager();
            var p = NamedParam("bad-tool", (ESQLToolType)999);
            Assert.Throws<NotSupportedException>(() => manager.Create<CfgEntity>(p));
        }

        [Fact]
        public void Create_NullParam_Throws()
        {
            var manager = new SQLManager();
            Assert.Throws<ArgumentNullException>(() => manager.Create<CfgEntity>(null!));
        }

        #endregion
    }
}
