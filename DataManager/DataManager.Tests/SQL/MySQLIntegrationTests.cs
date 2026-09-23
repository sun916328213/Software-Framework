using DataManager.SQL;
using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;
using MySqlConnector;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// MySQL 真库集成测试（连本机 3306，库表自建自清）。
    /// 连接可用环境变量覆盖：DM_MYSQL_HOST/DM_MYSQL_PORT/DM_MYSQL_USER/DM_MYSQL_PASSWORD/DM_MYSQL_DB，
    /// 默认 127.0.0.1:3306/root/mysql/dm_integration。
    /// </summary>
    public class MySQLIntegrationTests : IDisposable
    {
        public class MyDapperItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        public class MyEfItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        public class MySugarItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        [DapperSplitTable(SplitType.Day)]
        public class MyDapperSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        [DbSplitTable(SplitPeriod.Day)]
        public class MyEfSplitItem
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
            Name = $"my-it-{name}-{tool}",
            ServerIP = Env("DM_MYSQL_HOST", "127.0.0.1"),
            Port = Env("DM_MYSQL_PORT", "3306"),
            UserName = Env("DM_MYSQL_USER", "root"),
            Password = Env("DM_MYSQL_PASSWORD", "mysql"),
            DBName = Env("DM_MYSQL_DB", "dm_integration"),
            DBType = ESQLType.MySQL,
            ToolType = tool,
        };

        private static string AdminConnectionString()
        {
            var p = ParamFor(ESQLToolType.Dapper, "admin");
            return $"Server={p.ServerIP};Port={p.Port};Database={p.DBName};Uid={p.UserName};Pwd={p.Password};";
        }

        private static void DropTablesLike(params string[] prefixes)
        {
            using var conn = new MySqlConnection(AdminConnectionString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'";
            var names = new List<string>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    names.Add(reader.GetString(0));
            }
            foreach (var table in names)
            {
                if (prefixes.Any(p => table.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    using var drop = conn.CreateCommand();
                    drop.CommandText = $"DROP TABLE IF EXISTS `{table}`";
                    drop.ExecuteNonQuery();
                }
            }
        }

        [Fact]
        public void Dapper_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<MyDapperItem>(ParamFor(ESQLToolType.Dapper, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new MyDapperItem { Name = "IT1", Age = 21 });

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "IT1");
                Assert.Single(db.Query(x => x.Name == "IT1"));

                db.Update(x => x.Name == "IT1", new MyDapperItem { Name = "IT1", Age = 22 });
                Assert.Equal(22, db.Query(x => x.Name == "IT1").First().Age);

                db.Delete(x => x.Name == "IT1");
                Assert.Empty(db.Query(x => x.Name == "IT1"));
            }
            finally { db.DisConnect(); }
        }

        [Fact]
        public void Dapper_SplitTable_AutoRoutesByDate()
        {
            var manager = new SQLManager();
            var db = (DapperBase<MyDapperSplitItem>)manager.Create<MyDapperSplitItem>(
                ParamFor(ESQLToolType.Dapper, "split"));
            db.Connect();
            try
            {
                db.DeleteAll();
                var today = DateTime.Today;
                db.Add(new MyDapperSplitItem { Name = "T0", CreateTime = today });
                db.Add(new MyDapperSplitItem { Name = "T1", CreateTime = today.AddDays(-1) });

                var tables = db.GetSplitTables();
                Assert.True(tables.Count >= 2, $"期望至少2张分表，实际: {string.Join(",", tables)}");

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "T0");
                Assert.Contains(all, x => x.Name == "T1");
            }
            finally { db.DisConnect(); }
        }

        [Fact]
        public void EFCore_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<MyEfItem>(ParamFor(ESQLToolType.EFCore, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new MyEfItem { Name = "EF1", Age = 31 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "EF1");
                Assert.Single(db.Query(x => x.Name == "EF1"));

                db.Update(x => x.Name == "EF1", new MyEfItem { Name = "EF1", Age = 32 });
                Assert.Equal(32, db.Query(x => x.Name == "EF1").First().Age);

                db.Delete(x => x.Name == "EF1");
                Assert.Empty(db.Query(x => x.Name == "EF1"));
            }
            finally { db.DisConnect(); }
        }

        [Fact]
        public void EFCore_SplitTable_AutoRoutesByDate()
        {
            var manager = new SQLManager();
            var db = manager.Create<MyEfSplitItem>(ParamFor(ESQLToolType.EFCore, "split"));
            db.Connect();
            try
            {
                db.DeleteAll();
                var today = DateTime.Today;
                db.Add(new MyEfSplitItem { Name = "E0", CreateTime = today });
                db.Add(new MyEfSplitItem { Name = "E1", CreateTime = today.AddDays(-1) });

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "E0");
                Assert.Contains(all, x => x.Name == "E1");
            }
            finally { db.DisConnect(); }
        }

        [Fact]
        public void SqlSugar_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<MySugarItem>(ParamFor(ESQLToolType.SqlSugar, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new MySugarItem { Name = "SS1", Age = 41 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "SS1");

                db.Update(x => x.Name == "SS1", new MySugarItem { Name = "SS1", Age = 42 });
                Assert.Equal(42, db.Query(x => x.Name == "SS1").First().Age);

                db.Delete(x => x.Name == "SS1");
                Assert.Empty(db.Query(x => x.Name == "SS1"));
            }
            finally { db.DisConnect(); }
        }

        [Fact]
        public void Dapper_Transaction_CommitAndRollback()
        {
            var manager = new SQLManager();
            var db = manager.Create<MyDapperItem>(ParamFor(ESQLToolType.Dapper, "tx"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.ExecuteInTransaction(() =>
                {
                    db.Add(new MyDapperItem { Name = "W1", Age = 1 });
                    db.Add(new MyDapperItem { Name = "W2", Age = 2 });
                });
                Assert.Equal(2, db.Query().Count());

                Assert.Throws<InvalidOperationException>(() => db.ExecuteInTransaction(() =>
                {
                    db.Add(new MyDapperItem { Name = "W3", Age = 3 });
                    throw new InvalidOperationException("boom");
                }));
                Assert.DoesNotContain(db.Query().ToList(), x => x.Name == "W3");
            }
            finally { db.DisConnect(); }
        }

        public void Dispose()
        {
            try
            {
                DropTablesLike(
                    "mydapperitem", "mydappersplititem",
                    "MyEfItem", "MyEfSplitItem",
                    "mysugaritem");
            }
            catch { }
            GC.SuppressFinalize(this);
        }
    }
}
