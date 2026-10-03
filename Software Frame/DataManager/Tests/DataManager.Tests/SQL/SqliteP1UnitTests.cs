// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqliteP1UnitTests、BuilderEntity、BuilderTableAttr、NoKeyEntity、DapperMarkedSplit、SharedMarkedSplit、PlainEntity、NamingSample、CfgEntity。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DapperSplitType = DataManager.Runtime.SQL.Dapper.SplitType;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P1 纯单测：G-7 建库 SQL / G-8 Validate 矩阵 / G-9 DapperSqlBuilder /
    /// G-10 DapperSplitRouter 守卫 / G-11 DapperNaming / G-12 引号方言 / G-16 SQLManager。
    /// </summary>
    public class SqliteP1UnitTests
    {
        #region G-7 BuildCheckDatabaseExistsSql / BuildCreateDatabaseSql

        /// <summary>
        /// 验证 BuildCheckDatabaseExistsSql_KnownTypes_ContainsCatalog 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 BuildCheckDatabaseExistsSql、Contains；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 BuildCheckDatabaseExistsSql_Sqlite_IsSelectOne 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、BuildCheckDatabaseExistsSql；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildCheckDatabaseExistsSql_Sqlite_IsSelectOne()
        {
            Assert.Equal("SELECT 1", SqlDialectHelper.BuildCheckDatabaseExistsSql("x", ESQLType.SQLite));
        }

        /// <summary>
        /// 验证 BuildCheckDatabaseExistsSql_Unsupported_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、BuildCheckDatabaseExistsSql；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildCheckDatabaseExistsSql_Unsupported_Throws()
        {
            Assert.Throws<NotSupportedException>(
                () => SqlDialectHelper.BuildCheckDatabaseExistsSql("x", (ESQLType)999));
        }

        /// <summary>
        /// 验证 BuildCreateDatabaseSql_KnownServerTypes_ContainsCreate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 BuildCreateDatabaseSql、Contains；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 BuildCreateDatabaseSql_Sqlite_Unsupported_OrNoCreate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 BuildCreateDatabaseSql、DoesNotContain；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 验证 Validate_ConnectionStringPassthrough_OnlyNeedsName 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Validate；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Validate_ConnectionStringPassthrough_OnlyNeedsName()
        {
            var p = new SQLParam { Name = "n", ConnectionString = "Host=x", DBType = ESQLType.MySQL };
            p.Validate();
        }

        /// <summary>
        /// 验证 Validate_Sqlite_FilePathOrDbName_OK 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Validate、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Validate_Sqlite_FilePathOrDbName_OK()
        {
            new SQLParam { Name = "n", DBType = ESQLType.SQLite, FilePath = "a.db" }.Validate();
            new SQLParam { Name = "n", DBType = ESQLType.SQLite, DBName = "b.db" }.Validate();
            Assert.Throws<ArgumentException>(() =>
                new SQLParam { Name = "n", DBType = ESQLType.SQLite }.Validate());
        }

        /// <summary>
        /// 验证 Validate_MissingServerIP_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Validate_PostgresMissingPort_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Validate_SqlServer_PortOptional 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Validate；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Validate_MySQL_MissingDbName_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Validate_MySQL_MissingUserName_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Validate_NullPassword_Throws_EmptyPassword_OK 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Validate_Complete_Postgres_OK 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Validate；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Validate_Complete_Postgres_OK()
        {
            new SQLParam
            {
                Name = "n", DBType = ESQLType.PostgreSQL, ServerIP = "h", Port = "5432",
                DBName = "d", UserName = "u", Password = "p",
            }.Validate();
        }

        /// <summary>
        /// 验证 EffectiveFilePath_PrefersFilePath_OverDbName 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// BuilderEntity 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class BuilderEntity
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public DateTime? Birth { get; set; }
        }

        /// <summary>
        /// BuilderTableAttr 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [Table("ExplicitTable")]
        public class BuilderTableAttr
        {
            [Key]
            public int Id { get; set; }
            [Column("RenamedCol")]
            public string Value { get; set; } = string.Empty;
        }

        /// <summary>
        /// 验证 LiteParam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        private static SQLParam LiteParam(ESQLType dbType = ESQLType.SQLite) => new()
        {
            Name = "b", DBType = dbType, FilePath = "x.db", DBName = "x.db",
        };

        /// <summary>
        /// 验证 DapperSqlBuilder_GenerateCreateTableSql_Sqlite_UsesIfNotExistsAndQuotes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GenerateCreateTableSql、LiteParam、StartsWith、Contains；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 DapperSqlBuilder_GenerateCreateTableSql_SqlServer_NoIfNotExists 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 LiteParam、GenerateCreateTableSql、DoesNotContain、StartsWith、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DapperSqlBuilder_GenerateCreateTableSql_SqlServer_NoIfNotExists()
        {
            var p = LiteParam(ESQLType.SQL_Server);
            var sql = DapperSqlBuilder<BuilderEntity>.GenerateCreateTableSql(p, "builderentity");
            Assert.DoesNotContain("IF NOT EXISTS", sql);
            Assert.StartsWith("CREATE TABLE", sql);
            Assert.Contains("[builderentity]", sql);
        }

        /// <summary>
        /// 验证 DapperSqlBuilder_BuildSelectAndDeleteSql_QuoteByDialect 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 LiteParam、Equal、BuildSelectAllSql、BuildDeleteAllSql；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 DapperSqlBuilder_BuildInsertSql_ListsColumnsAndParameters 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 EntityProperties、BuildInsertSql、LiteParam、Contains；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 DapperSqlBuilder_FindKeyProperty_And_IsDefaultKeyValue 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FindKeyProperty、NotNull、Equal、True、IsDefaultKeyValue；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 DapperSqlBuilder_BuildUpdateByKeySql_SetsAndWhere 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 EntityProperties、FindKeyProperty、Where、BuildUpdateByKeySql、LiteParam；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 DapperSqlBuilder_IsNullableType_Matrix 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 True、IsNullableType、False；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// DapperMarkedSplit 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(DapperSplitType.Day)]
        public class DapperMarkedSplit
        {
            [Key]
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// SharedMarkedSplit 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Day)]
        public class SharedMarkedSplit
        {
            [Key]
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// PlainEntity 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class PlainEntity
        {
            [Key]
            public long Id { get; set; }
        }

        /// <summary>
        /// 验证 RouterParam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        private static SQLParam RouterParam() => new()
        {
            Name = "r", DBType = ESQLType.SQLite, FilePath = "x.db", DBName = "x.db",
        };

        /// <summary>
        /// 验证 DapperSplitRouter_IsSplitTableEntity_BothAttributes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 True、IsSplitTableEntity、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DapperSplitRouter_IsSplitTableEntity_BothAttributes()
        {
            Assert.True(DapperSplitRouter<DapperMarkedSplit>.IsSplitTableEntity());
            Assert.True(DapperSplitRouter<SharedMarkedSplit>.IsSplitTableEntity());
            Assert.False(DapperSplitRouter<PlainEntity>.IsSplitTableEntity());
        }

        /// <summary>
        /// 验证 DapperSplitRouter_GetSplitTableName_LegacyAndShared 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RouterParam、Equal、GetSplitTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DapperSplitRouter_GetSplitTableName_LegacyAndShared()
        {
            var date = new DateTime(2026, 9, 23);
            var legacy = new DapperSplitRouter<DapperMarkedSplit>(RouterParam());
            Assert.Equal("DapperMarkedSplit_20260923", legacy.GetSplitTableName(date));

            var shared = new DapperSplitRouter<SharedMarkedSplit>(RouterParam());
            Assert.Equal("SharedMarkedSplit_20260923", shared.GetSplitTableName(date));
        }

        /// <summary>
        /// 验证 DapperSplitRouter_NonSplit_GetSplitTableName_ReturnsTypeName 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RouterParam、Equal、GetSplitTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DapperSplitRouter_NonSplit_GetSplitTableName_ReturnsTypeName()
        {
            var r = new DapperSplitRouter<PlainEntity>(RouterParam());
            Assert.Equal("PlainEntity", r.GetSplitTableName(DateTime.Today));
        }

        /// <summary>
        /// 验证 DapperSplitRouter_TxPath_WithoutTx_Ok 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RouterParam、Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DapperSplitRouter_TxPath_WithoutTx_Ok()
        {
            var r = new DapperSplitRouter<PlainEntity>(RouterParam());
            Assert.Null(r.Tx);
        }

        /// <summary>
        /// 验证 DapperSplitRouter_NoKeyEntity_FindKeyProperty_IsNull 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Null、FindKeyProperty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DapperSplitRouter_NoKeyEntity_FindKeyProperty_IsNull()
        {
            // 无 [Key] 时 FindKeyProperty 返回 null；EnsureTable/插入路径应据此抛错。
            Assert.Null(DapperSqlBuilder<NoKeyEntity>.FindKeyProperty());
        }

        #endregion

        #region G-11 DapperNaming

        /// <summary>
        /// NamingSample 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [Table("ExplicitTableName")]
        public class NamingSample
        {
            public long Id { get; set; }
            [Column("ExplicitColumn")]
            public string Name { get; set; } = string.Empty;
            public int PlainCol { get; set; }
        }

        /// <summary>
        /// 验证 LowerTableNameResolver_Lowercases_WithOrWithoutAttribute 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ResolveTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void LowerTableNameResolver_Lowercases_WithOrWithoutAttribute()
        {
            var resolver = new LowerTableNameResolver();
            Assert.Equal("explicittablename", resolver.ResolveTableName(typeof(NamingSample)));
            Assert.Equal("plainentity", resolver.ResolveTableName(typeof(PlainEntity)));
        }

        /// <summary>
        /// 验证 LowerColumnNameResolver_Lowercases_WithOrWithoutAttribute 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GetProperty、nameof、Equal、ResolveColumnName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void LowerColumnNameResolver_Lowercases_WithOrWithoutAttribute()
        {
            var resolver = new LowerColumnNameResolver();
            var withAttr = typeof(NamingSample).GetProperty(nameof(NamingSample.Name))!;
            var plain = typeof(NamingSample).GetProperty(nameof(NamingSample.PlainCol))!;
            Assert.Equal("explicitcolumn", resolver.ResolveColumnName(withAttr));
            Assert.Equal("plaincol", resolver.ResolveColumnName(plain));
        }

        /// <summary>
        /// 验证 DapperDommelSetup_Ensure_IsIdempotent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Ensure；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 QuoteTable_SqlServer_Brackets_Lowercased 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、WrapTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void QuoteTable_SqlServer_Brackets_Lowercased()
        {
            Assert.Equal("[mytable]", SqlDialectHelper.WrapTableName("MyTable", ESQLType.SQL_Server));
        }

        /// <summary>
        /// 验证 QuoteTable_MySQL_Backticks_Lowercased 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、WrapTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void QuoteTable_MySQL_Backticks_Lowercased()
        {
            Assert.Equal("`mytable`", SqlDialectHelper.WrapTableName("MyTable", ESQLType.MySQL));
        }

        /// <summary>
        /// 验证 QuoteTable_Postgres_NoQuotes_Lowercased 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、WrapTableName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void QuoteTable_Postgres_NoQuotes_Lowercased()
        {
            // PG 方言 QuoteTable 仅小写、不加引号（标识符默认合规时无需引号）。
            Assert.Equal("mytable", SqlDialectHelper.WrapTableName("MyTable", ESQLType.PostgreSQL));
        }

        /// <summary>
        /// 验证 FormatTableName_And_ColumnList_PerDialect 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、FormatTableName、FormatColumnList；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 ConvertTableAndColumn_AlwaysLowerInvariant 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ConvertTableName、ConvertColumnName、True、NeedsConversion；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 NamedParam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
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

        /// <summary>
        /// 验证 Config_List_ThrowsNotSupported_UntilTypedPathExists 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Config、NamedParam、Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Create_UnknownToolType_ThrowsNotSupported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 NamedParam、Throws、Create；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_UnknownToolType_ThrowsNotSupported()
        {
            var manager = new SQLManager();
            var p = NamedParam("bad-tool", (ESQLToolType)999);
            Assert.Throws<NotSupportedException>(() => manager.Create<CfgEntity>(p));
        }

        /// <summary>
        /// 验证 Create_NullParam_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、Create；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Create_NullParam_Throws()
        {
            var manager = new SQLManager();
            Assert.Throws<ArgumentNullException>(() => manager.Create<CfgEntity>(null!));
        }

        #endregion
    }
}
