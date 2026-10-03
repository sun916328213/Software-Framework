// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PostgresIntegrationTests、PgDapperItem、PgEfItem、PgSugarItem、PgDapperSplitItem、PgEfSplitItem。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Runtime.SQL.EFCore;
using DataManager.Runtime.SQL.SqlSugar;
using Npgsql;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// PostgreSQL 真库集成测试（连本机 PG，库表自建自清）。
    /// 连接信息可用环境变量覆盖：DM_PG_HOST/DM_PG_PORT/DM_PG_USER/DM_PG_PASSWORD/DM_PG_DB，
    /// 默认 127.0.0.1:5432/postgres/postgres/dm_integration。
    /// </summary>
    [Trait("Category", "ExternalDatabase")]
    public class PostgresIntegrationTests : IDisposable
    {
        /// <summary>
        /// 初始化 PostgresIntegrationTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 SetSwitch；具体条件和先后次序见方法体。
        /// </summary>
        static PostgresIntegrationTests()
        {
            // 与 DataManagerModule 一致：允许写入 Local 时间（测试用 DateTime.Now/Today）
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        /// <summary>
        /// PgDapperItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class PgDapperItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// PgEfItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class PgEfItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// PgSugarItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class PgSugarItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// PgDapperSplitItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(SplitType.Day)]
        public class PgDapperSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// PgEfSplitItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Day)]
        public class PgEfSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// 验证 Env 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GetEnvironmentVariable；具体条件和先后次序见方法体。
        /// </summary>
        private static string Env(string key, string fallback)
            => Environment.GetEnvironmentVariable(key) ?? fallback;

        /// <summary>
        /// 验证 ParamFor 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Env；具体条件和先后次序见方法体。
        /// </summary>
        private static SQLParam ParamFor(ESQLToolType tool, string name) => new()
        {
            Name = $"pg-it-{name}-{tool}",
            ServerIP = Env("DM_PG_HOST", "127.0.0.1"),
            Port = Env("DM_PG_PORT", "5432"),
            UserName = Env("DM_PG_USER", "postgres"),
            Password = Env("DM_PG_PASSWORD", "postgres"),
            DBName = Env("DM_PG_DB", "dm_integration"),
            DBType = ESQLType.PostgreSQL,
            ToolType = tool,
        };

        /// <summary>
        /// 验证 AdminConnectionString 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ParamFor；具体条件和先后次序见方法体。
        /// </summary>
        private static string AdminConnectionString()
        {
            var p = ParamFor(ESQLToolType.Dapper, "admin");
            return $"Host={p.ServerIP};Port={p.Port};Database=postgres;Username={p.UserName};Password={p.Password}";
        }

        /// <summary>
        /// 验证 DropTablesLike 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ParamFor、AdminConnectionString、Open、CreateCommand、ToLower；具体条件和先后次序见方法体。
        /// </summary>
        private static void DropTablesLike(params string[] prefixes)
        {
            var p = ParamFor(ESQLToolType.Dapper, "admin");
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
        /// 验证 Dapper_CRUD_RoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<PgDapperItem>(ParamFor(ESQLToolType.Dapper, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new PgDapperItem { Name = "IT1", Age = 21 });

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "IT1");

                var one = db.Query(x => x.Name == "IT1").ToList();
                Assert.Single(one);

                db.Update(x => x.Name == "IT1", new PgDapperItem { Name = "IT1", Age = 22 });
                Assert.Equal(22, db.Query(x => x.Name == "IT1").First().Age);

                db.Delete(x => x.Name == "IT1");
                Assert.Empty(db.Query(x => x.Name == "IT1"));
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 验证 Dapper_SplitTable_AutoRoutesByDate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_SplitTable_AutoRoutesByDate()
        {
            var manager = new SQLManager();
            var db = (DapperBase<PgDapperSplitItem>)manager.Create<PgDapperSplitItem>(ParamFor(ESQLToolType.Dapper, "split"));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new PgDapperSplitItem { Name = "T0", CreateTime = today });
                db.Add(new PgDapperSplitItem { Name = "T1", CreateTime = today.AddDays(-1) });

                var tables = db.GetSplitTables();
                Assert.True(tables.Count >= 2, $"期望至少2张分表，实际: {string.Join(",", tables)}");

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "T0");
                Assert.Contains(all, x => x.Name == "T1");
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 验证 EFCore_CRUD_RoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<PgEfItem>(ParamFor(ESQLToolType.EFCore, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new PgEfItem { Name = "EF1", Age = 31 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "EF1");
                Assert.Single(db.Query(x => x.Name == "EF1"));

                db.Update(x => x.Name == "EF1", new PgEfItem { Name = "EF1", Age = 32 });
                Assert.Equal(32, db.Query(x => x.Name == "EF1").First().Age);

                db.Delete(x => x.Name == "EF1");
                Assert.Empty(db.Query(x => x.Name == "EF1"));
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 验证 EFCore_SplitTable_AutoRoutesByDate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_SplitTable_AutoRoutesByDate()
        {
            var manager = new SQLManager();
            var db = manager.Create<PgEfSplitItem>(ParamFor(ESQLToolType.EFCore, "split"));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new PgEfSplitItem { Name = "E0", CreateTime = today });
                db.Add(new PgEfSplitItem { Name = "E1", CreateTime = today.AddDays(-1) });

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "E0");
                Assert.Contains(all, x => x.Name == "E1");
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 验证 SqlSugar_CRUD_RoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<PgSugarItem>(ParamFor(ESQLToolType.SqlSugar, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new PgSugarItem { Name = "SS1", Age = 41 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "SS1");

                db.Update(x => x.Name == "SS1", new PgSugarItem { Name = "SS1", Age = 42 });
                Assert.Equal(42, db.Query(x => x.Name == "SS1").First().Age);

                db.Delete(x => x.Name == "SS1");
                Assert.Empty(db.Query(x => x.Name == "SS1"));
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 DropTablesLike、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try { DropTablesLike("pgdapperitem", "pgdappersplititem", "PgEfSplitItem", "PGEfItem", "pgsugaritem"); }
            catch { }
            GC.SuppressFinalize(this);
        }
    }
}
