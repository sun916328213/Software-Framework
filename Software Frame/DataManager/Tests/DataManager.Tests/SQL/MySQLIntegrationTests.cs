// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MySQLIntegrationTests、MyDapperItem、MyEfItem、MySugarItem、MyDapperSplitItem、MyEfSplitItem。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Runtime.SQL.EFCore;
using DataManager.Runtime.SQL.SqlSugar;
using MySqlConnector;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// MySQL 真库集成测试（连本机 3306，库表自建自清）。
    /// 连接可用环境变量覆盖：DM_MYSQL_HOST/DM_MYSQL_PORT/DM_MYSQL_USER/DM_MYSQL_PASSWORD/DM_MYSQL_DB，
    /// 默认 127.0.0.1:3306/root/mysql/dm_integration。
    /// </summary>
    [Trait("Category", "ExternalDatabase")]
    public class MySQLIntegrationTests : IDisposable
    {
        /// <summary>
        /// MyDapperItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class MyDapperItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// MyEfItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class MyEfItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// MySugarItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class MySugarItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// MyDapperSplitItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(SplitType.Day)]
        public class MyDapperSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// MyEfSplitItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Day)]
        public class MyEfSplitItem
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
            Name = $"my-it-{name}-{tool}",
            ServerIP = Env("DM_MYSQL_HOST", "127.0.0.1"),
            Port = Env("DM_MYSQL_PORT", "3306"),
            UserName = Env("DM_MYSQL_USER", "root"),
            Password = Env("DM_MYSQL_PASSWORD", "mysql"),
            DBName = Env("DM_MYSQL_DB", "dm_integration"),
            DBType = ESQLType.MySQL,
            ToolType = tool,
        };

        /// <summary>
        /// 验证 AdminConnectionString 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ParamFor；具体条件和先后次序见方法体。
        /// </summary>
        private static string AdminConnectionString()
        {
            var p = ParamFor(ESQLToolType.Dapper, "admin");
            return $"Server={p.ServerIP};Port={p.Port};Database={p.DBName};Uid={p.UserName};Pwd={p.Password};";
        }

        /// <summary>
        /// 验证 DropTablesLike 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 AdminConnectionString、Open、CreateCommand、ExecuteReader、Read；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Dapper_CRUD_RoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Dapper_SplitTable_AutoRoutesByDate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 EFCore_CRUD_RoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 EFCore_SplitTable_AutoRoutesByDate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 SqlSugar_CRUD_RoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Dapper_Transaction_CommitAndRollback 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、ExecuteInTransaction；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 DropTablesLike、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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
