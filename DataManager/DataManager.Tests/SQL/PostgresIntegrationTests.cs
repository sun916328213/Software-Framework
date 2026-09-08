using DataManager.SQL;
using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;
using Npgsql;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// PostgreSQL 真库集成测试（连本机 PG，库表自建自清）。
    /// 连接信息可用环境变量覆盖：DM_PG_HOST/DM_PG_PORT/DM_PG_USER/DM_PG_PASSWORD/DM_PG_DB，
    /// 默认 127.0.0.1:5432/postgres/postgres/dm_integration。
    /// </summary>
    public class PostgresIntegrationTests : IDisposable
    {
        static PostgresIntegrationTests()
        {
            // 与 DataManagerModule 一致：允许写入 Local 时间（测试用 DateTime.Now/Today）
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public class PgDapperItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        public class PgEfItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        public class PgSugarItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        [DapperSplitTable(SplitType.Day)]
        public class PgDapperSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        [DbSplitTable(SplitPeriod.Day)]
        public class PgEfSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        private static string Env(string key, string fallback)
            => Environment.GetEnvironmentVariable(key) ?? fallback;

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

        private static string AdminConnectionString()
        {
            var p = ParamFor(ESQLToolType.Dapper, "admin");
            return $"Host={p.ServerIP};Port={p.Port};Database=postgres;Username={p.UserName};Password={p.Password}";
        }

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

        public void Dispose()
        {
            try { DropTablesLike("pgdapperitem", "pgdappersplititem", "PgEfSplitItem", "PGEfItem", "pgsugaritem"); }
            catch { }
            GC.SuppressFinalize(this);
        }
    }
}
