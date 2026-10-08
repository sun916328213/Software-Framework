// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PostgresFeatureTests、PgPushItem、PgTxItem、PgOneItem、PgEfPruneItem、PgSugarPruneItem。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using Npgsql;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using DapperSplitType = DataManager.Runtime.SQL.Dapper.SplitType;
using SugarSplitType = SqlSugar.SplitType;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P12：新功能 PG 方言验证（下推/裁剪/事务/单更新/分表列表）。
    /// SQLite 已覆盖逻辑，此处锁定 PG 特有的 SQL 形态（无引号小写、BIGSERIAL、information_schema）。
    /// 连接约定同 PostgresIntegrationTests：DM_PG_*，默认 127.0.0.1:5432/postgres/postgres/dm_integration。
    /// </summary>
    [Trait("Category", "ExternalDatabase")]
    public class PostgresFeatureTests : IDisposable
    {
        /// <summary>
        /// 初始化 PostgresFeatureTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 SetSwitch；具体条件和先后次序见方法体。
        /// </summary>
        static PostgresFeatureTests()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        /// <summary>
        /// PgPushItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(DapperSplitType.Day)]
        public class PgPushItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// PgTxItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class PgTxItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// PgOneItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class PgOneItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// PgEfPruneItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Day)]
        public class PgEfPruneItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// PgSugarPruneItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [SugarTable("pgfeatsplit_{year}{month}{day}")]
        [SplitTable(SugarSplitType.Day)]
        public class PgSugarPruneItem
        {
            [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            [SplitField]
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// 测试相关入口 Env；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetEnvironmentVariable；具体条件和先后次序见方法体。
        /// </summary>
        private static string Env(string key, string fallback)
            => Environment.GetEnvironmentVariable(key) ?? fallback;

        /// <summary>
        /// 测试相关入口 ParamFor；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Env；具体条件和先后次序见方法体。
        /// </summary>
        private static SQLParam ParamFor(ESQLToolType tool, string name) => new()
        {
            Name = $"pg-feat-{name}-{tool}",
            ServerIP = Env("DM_PG_HOST", "127.0.0.1"),
            Port = Env("DM_PG_PORT", "5432"),
            UserName = Env("DM_PG_USER", "postgres"),
            Password = Env("DM_PG_PASSWORD", "postgres"),
            DBName = Env("DM_PG_DB", "dm_integration"),
            DBType = ESQLType.PostgreSQL,
            ToolType = tool,
        };

        /// <summary>
        /// 测试相关入口 AdminConnectionString；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ParamFor；具体条件和先后次序见方法体。
        /// </summary>
        private static string AdminConnectionString()
        {
            // P12修正：清表必须连测试库本身；连 postgres 系统库的旧写法是空转（残留跨 run 污染）。
            var p = ParamFor(ESQLToolType.Dapper, "admin");
            return $"Host={p.ServerIP};Port={p.Port};Database={p.DBName};Username={p.UserName};Password={p.Password}";
        }

        /// <summary>
        /// 测试相关入口 DropTablesLike；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 AdminConnectionString、Open、CreateCommand、ToLower、ExecuteNonQuery；具体条件和先后次序见方法体。
        /// </summary>
        private static void DropTablesLike(params string[] prefixes)
        {
            using var conn = new NpgsqlConnection(AdminConnectionString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            foreach (var prefix in prefixes)
            {
                cmd.CommandText =
                    $"DO $$ DECLARE r RECORD; BEGIN FOR r IN SELECT tablename FROM pg_tables " +
                    $"WHERE schemaname='public' AND lower(tablename) LIKE '{prefix.ToLower()}%' LOOP " +
                    $"EXECUTE 'DROP TABLE IF EXISTS \"' || r.tablename || '\"'; END LOOP; END $$;";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 测试相关入口 Dapper_SplitPushdownAndPrune_CorrectOnPg；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_SplitPushdownAndPrune_CorrectOnPg()
        {
            var manager = new SQLManager();
            var db = (DapperBase<PgPushItem>)manager.Create<PgPushItem>(ParamFor(ESQLToolType.Dapper, "push"));
            db.Connect();
            try
            {
                // 先清：跨 run 残留会污染精确断言（分表 DeleteAll 覆盖全表，可靠）。
                db.DeleteAll();
                var today = DateTime.Today;
                db.Add(new PgPushItem { Name = "Q1", Age = 21, CreateTime = today });
                db.Add(new PgPushItem { Name = "Q2", Age = 35, CreateTime = today.AddDays(-1) });
                Assert.True(db.GetSplitTables().Count >= 2);

                // 下推 + 裁剪：只要今天表 + Age 条件。
                var hit = db.Query(x => x.CreateTime == today && x.Age > 30).ToList();
                Assert.Empty(hit);
                var hit2 = db.Query(x => x.CreateTime == today && x.Age > 20).Select(x => x.Name).ToList();
                Assert.Equal(["Q1"], hit2);
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 Dapper_Transaction_RollbackOnPg；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、ExecuteInTransaction；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_Transaction_RollbackOnPg()
        {
            var manager = new SQLManager();
            var db = manager.Create<PgTxItem>(ParamFor(ESQLToolType.Dapper, "tx"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.ExecuteInTransaction(() =>
                {
                    db.Add(new PgTxItem { Name = "W1", Age = 1 });
                    db.Add(new PgTxItem { Name = "W2", Age = 2 });
                });
                Assert.Equal(2, db.Query().Count());

                Assert.Throws<InvalidOperationException>(() => db.ExecuteInTransaction(() =>
                {
                    db.Add(new PgTxItem { Name = "W3", Age = 3 });
                    throw new InvalidOperationException("boom");
                }));
                Assert.DoesNotContain(db.Query().ToList(), x => x.Name == "W3");
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 Dapper_SingleUpdate_RoundTripOnPg；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_SingleUpdate_RoundTripOnPg()
        {
            var manager = new SQLManager();
            var db = manager.Create<PgOneItem>(ParamFor(ESQLToolType.Dapper, "one"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new PgOneItem { Name = "O1", Age = 10 });
                var one = db.Query(x => x.Name == "O1").First();
                one.Age = 11;
                db.Update(one);
                Assert.Equal(11, db.Query(x => x.Name == "O1").First().Age);
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 EFCore_SplitPrune_CorrectOnPg；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_SplitPrune_CorrectOnPg()
        {
            var manager = new SQLManager();
            var db = manager.Create<PgEfPruneItem>(ParamFor(ESQLToolType.EFCore, "prune"));
            db.Connect();
            try
            {
                db.DeleteAll();
                var today = DateTime.Today;
                db.Add(new PgEfPruneItem { Name = "F1", CreateTime = today });
                db.Add(new PgEfPruneItem { Name = "F2", CreateTime = today.AddDays(-1) });

                // EF 表名保留大小写，裁剪必须精确命中。
                var hit = db.Query(x => x.CreateTime == today).Select(x => x.Name).ToList();
                Assert.Equal(["F1"], hit);
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_SplitList_CorrectOnPg；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_SplitList_CorrectOnPg()
        {
            var manager = new SQLManager();
            var db = (DataManager.Runtime.SQL.SqlSugarSQL.SqlSugarBase<PgSugarPruneItem>)manager.Create<PgSugarPruneItem>(ParamFor(ESQLToolType.SqlSugar, "list"));
            db.Connect();
            try
            {
                // SqlSugar 分表 DeleteAll 覆盖范围不确定：Id 用 run 内唯一值防跨 run 主键冲突。
                var uniq = DateTime.UtcNow.Ticks % 1_000_000_000;
                var today = DateTime.Today;
                db.Add(new PgSugarPruneItem { Id = uniq, Name = "G1", CreateTime = today });
                db.Add(new PgSugarPruneItem { Id = uniq + 1, Name = "G2", CreateTime = today.AddDays(-1) });

                var tables = db.GetSplitTables();
                Assert.True(tables.Count >= 2, $"期望至少2张分表，实际: {string.Join(",", tables)}");
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 DropTablesLike、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try { DropTablesLike("pgpushitem", "pgtxitem", "pgoneitem", "PgEfPruneItem", "pgfeatsplit"); }
            catch { }
            GC.SuppressFinalize(this);
        }
    }
}
