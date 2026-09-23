using DataManager.SQL;
using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// SQL Server 真库集成测试（连本机实例，库表自建自清）。
    /// 连接可用环境变量覆盖：DM_MSSQL_HOST（默认 localhost\BARTENDER）、
    /// DM_MSSQL_DB（默认 dm_integration）、DM_MSSQL_USER/DM_MSSQL_PASSWORD（可选，空则 Windows 身份验证）。
    /// </summary>
    public class SqlServerIntegrationTests : IDisposable
    {
        public class MsDapperItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        public class MsEfItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        public class MsSugarItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        [DapperSplitTable(SplitType.Day)]
        public class MsDapperSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        [DbSplitTable(SplitPeriod.Day)]
        public class MsEfSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        private static string Env(string key, string fallback)
            => Environment.GetEnvironmentVariable(key) ?? fallback;

        private static SQLParam ParamFor(ESQLToolType tool, string name)
        {
            var server = Env("DM_MSSQL_HOST", @"localhost\BARTENDER");
            var db = Env("DM_MSSQL_DB", "dm_integration");
            var user = Env("DM_MSSQL_USER", string.Empty);
            var password = Env("DM_MSSQL_PASSWORD", string.Empty);

            // 有账号走分字段；无账号走 Windows 身份验证直通串（Validate 只查 Name + DBType）。
            if (string.IsNullOrEmpty(user))
            {
                return new SQLParam
                {
                    Name = $"ms-it-{name}-{tool}",
                    ServerIP = server,
                    DBName = db,
                    DBType = ESQLType.SQL_Server,
                    ToolType = tool,
                    ConnectionString = $"Server={server};Database={db};Integrated Security=true;TrustServerCertificate=True;",
                };
            }

            return new SQLParam
            {
                Name = $"ms-it-{name}-{tool}",
                ServerIP = server,
                DBName = db,
                UserName = user,
                Password = password,
                DBType = ESQLType.SQL_Server,
                ToolType = tool,
            };
        }

        private static string AdminConnectionString()
        {
            var p = ParamFor(ESQLToolType.Dapper, "admin");
            return p.ConnectionString;
        }

        private static void DropTablesLike(params string[] prefixes)
        {
            using var conn = new SqlConnection(AdminConnectionString());
            conn.Open();
            var names = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    names.Add(reader.GetString(0));
            }
            foreach (var table in names)
            {
                if (prefixes.Any(p => table.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    using var drop = conn.CreateCommand();
                    drop.CommandText = $"DROP TABLE [{table}]";
                    drop.ExecuteNonQuery();
                }
            }
        }

        [Fact]
        public void Dapper_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<MsDapperItem>(ParamFor(ESQLToolType.Dapper, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new MsDapperItem { Name = "IT1", Age = 21 });

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "IT1");
                Assert.Single(db.Query(x => x.Name == "IT1"));

                db.Update(x => x.Name == "IT1", new MsDapperItem { Name = "IT1", Age = 22 });
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
            var db = (DapperBase<MsDapperSplitItem>)manager.Create<MsDapperSplitItem>(
                ParamFor(ESQLToolType.Dapper, "split"));
            db.Connect();
            try
            {
                db.DeleteAll();
                var today = DateTime.Today;
                db.Add(new MsDapperSplitItem { Name = "T0", CreateTime = today });
                db.Add(new MsDapperSplitItem { Name = "T1", CreateTime = today.AddDays(-1) });

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
            var db = manager.Create<MsEfItem>(ParamFor(ESQLToolType.EFCore, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new MsEfItem { Name = "EF1", Age = 31 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "EF1");
                Assert.Single(db.Query(x => x.Name == "EF1"));

                db.Update(x => x.Name == "EF1", new MsEfItem { Name = "EF1", Age = 32 });
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
            var db = manager.Create<MsEfSplitItem>(ParamFor(ESQLToolType.EFCore, "split"));
            db.Connect();
            try
            {
                db.DeleteAll();
                var today = DateTime.Today;
                db.Add(new MsEfSplitItem { Name = "E0", CreateTime = today });
                db.Add(new MsEfSplitItem { Name = "E1", CreateTime = today.AddDays(-1) });

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
            var db = manager.Create<MsSugarItem>(ParamFor(ESQLToolType.SqlSugar, "crud"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new MsSugarItem { Name = "SS1", Age = 41 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "SS1");

                db.Update(x => x.Name == "SS1", new MsSugarItem { Name = "SS1", Age = 42 });
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
            var db = manager.Create<MsDapperItem>(ParamFor(ESQLToolType.Dapper, "tx"));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.ExecuteInTransaction(() =>
                {
                    db.Add(new MsDapperItem { Name = "W1", Age = 1 });
                    db.Add(new MsDapperItem { Name = "W2", Age = 2 });
                });
                Assert.Equal(2, db.Query().Count());

                Assert.Throws<InvalidOperationException>(() => db.ExecuteInTransaction(() =>
                {
                    db.Add(new MsDapperItem { Name = "W3", Age = 3 });
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
                    "msdapperitem", "msdappersplititem",
                    "MsEfItem", "MsEfSplitItem",
                    "mssugaritem");
            }
            catch { }
            GC.SuppressFinalize(this);
        }
    }
}
