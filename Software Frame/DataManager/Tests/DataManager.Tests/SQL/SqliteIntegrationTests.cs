// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqliteIntegrationTests、LiteDapperItem、LiteEfItem、LiteSugarItem、LiteDapperSplitItem、LiteEfSplitItem、LiteEfMonthItem、LiteDapperFilterItem、LiteSugarSplitItem、LiteMonthItem。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Runtime.SQL.SqlSugarSQL;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using DapperSplitType = DataManager.Runtime.SQL.Dapper.SplitType;
using SugarSplitType = SqlSugar.SplitType;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// SQLite 真库测试（文件自建自清，无需服务器，CI 可跑）。
    /// 覆盖三工具 CRUD + Dapper 分表路由，证明 P3 双库实现端到端可用。
    /// </summary>
    public class SqliteIntegrationTests : IDisposable
    {
        /// <summary>
        /// LiteDapperItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class LiteDapperItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// LiteEfItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class LiteEfItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// LiteSugarItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class LiteSugarItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// LiteDapperSplitItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(DapperSplitType.Day)]
        public class LiteDapperSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// LiteEfSplitItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Day)]
        public class LiteEfSplitItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// LiteEfMonthItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Month)]
        public class LiteEfMonthItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        private readonly string _file = Path.Combine(
            Path.GetTempPath(), $"dm_sqlite_it_{Guid.NewGuid():N}.db");

        /// <summary>
        /// 测试相关入口 ParamFor；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewGuid；具体条件和先后次序见方法体。
        /// </summary>
        private SQLParam ParamFor<T>(ESQLToolType tool) => new()
        {
            Name = $"lite-it-{typeof(T).Name}-{tool}-{Guid.NewGuid():N}",
            FilePath = _file,
            DBName = _file,
            DBType = ESQLType.SQLite,
            ToolType = tool,
        };

        /// <summary>
        /// 测试相关入口 Dapper_CRUD_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<LiteDapperItem>(ParamFor<LiteDapperItem>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new LiteDapperItem { Name = "L1", Age = 21 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "L1");
                Assert.Single(db.Query(x => x.Name == "L1"));

                db.Update(x => x.Name == "L1", new LiteDapperItem { Name = "L1", Age = 22 });
                Assert.Equal(22, db.Query(x => x.Name == "L1").First().Age);

                db.Delete(x => x.Name == "L1");
                Assert.Empty(db.Query(x => x.Name == "L1"));
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 Dapper_SplitTable_AutoRoutesByDate；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_SplitTable_AutoRoutesByDate()
        {
            var manager = new SQLManager();
            var db = (DapperBase<LiteDapperSplitItem>)manager.Create<LiteDapperSplitItem>(ParamFor<LiteDapperSplitItem>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new LiteDapperSplitItem { Name = "T0", CreateTime = today });
                db.Add(new LiteDapperSplitItem { Name = "T1", CreateTime = today.AddDays(-1) });

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
        /// 测试相关入口 Dapper_SplitTable_DeleteAsync_And_DeleteAllAsync_CoverAllTables；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Dapper_SplitTable_DeleteAsync_And_DeleteAllAsync_CoverAllTables()
        {
            var manager = new SQLManager();
            var db = (DapperBase<LiteDapperSplitItem>)manager.Create<LiteDapperSplitItem>(ParamFor<LiteDapperSplitItem>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new LiteDapperSplitItem { Name = "D0", CreateTime = today });
                db.Add(new LiteDapperSplitItem { Name = "D1", CreateTime = today.AddDays(-1) });

                // P4-2 回归：异步条件删除必须覆盖分表（旧版静默只删默认表）。
                await db.DeleteAsync(x => x.Name == "D0");
                var rest = (await db.QueryAsync()).ToList();
                Assert.DoesNotContain(rest, x => x.Name == "D0");
                Assert.Contains(rest, x => x.Name == "D1");

                // P4-2 回归：异步清空必须覆盖分表（旧版只删基表名）。
                await db.DeleteAllAsync();
                Assert.Empty(await db.QueryAsync());
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 Dapper_SplitTable_AddAsync_RoutesByDate；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、AddAsync、Contains；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Dapper_SplitTable_AddAsync_RoutesByDate()
        {
            var manager = new SQLManager();
            var db = (DapperBase<LiteDapperSplitItem>)manager.Create<LiteDapperSplitItem>(ParamFor<LiteDapperSplitItem>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                // P4-2 对齐：单条异步写入同样按日期路由（旧版直写默认表，与同步 Add 不一致）。
                await db.AddAsync(new LiteDapperSplitItem { Name = "A0", CreateTime = DateTime.Today });
                Assert.Contains((await db.QueryAsync()).ToList(), x => x.Name == "A0");
                await db.DeleteAllAsync();
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// LiteDapperFilterItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(DapperSplitType.Day)]
        public class LiteDapperFilterItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// LiteSugarSplitItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [SugarTable("litesugarsplit_{year}{month}{day}")]
        [SplitTable(SugarSplitType.Day)]
        public class LiteSugarSplitItem
        {
            // SqlSugar 原生约束：分表禁自增（跨表撞号），主键显式赋值。
            [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            [SplitField]
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// 测试相关入口 Dapper_SplitTable_PredicatePushdown_MatchesMemoryFilter；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_SplitTable_PredicatePushdown_MatchesMemoryFilter()
        {
            // P6-2：下推路径与回退路径结果一致（跨两张分表）。
            var manager = new SQLManager();
            var db = (DapperBase<LiteDapperFilterItem>)manager.Create<LiteDapperFilterItem>(ParamFor<LiteDapperFilterItem>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new LiteDapperFilterItem { Name = "F1", Age = 21, CreateTime = today });
                db.Add(new LiteDapperFilterItem { Name = "F2", Age = 35, CreateTime = today.AddDays(-1) });
                db.Add(new LiteDapperFilterItem { Name = "F3", Age = 40, CreateTime = today });
                Assert.True(db.GetSplitTables().Count >= 2);

                // 可译：Age > 30 → 服务端过滤。
                var pushed = db.Query(x => x.Age > 30).Select(x => x.Name).Order().ToList();
                Assert.Equal(["F2", "F3"], pushed);

                // 不可译（含方法调用）→ 回退内存过滤，结果仍正确。
                var fallback = db.Query(x => x.Name.Contains("F") && x.Age > 30).Select(x => x.Name).Order().ToList();
                Assert.Equal(["F2", "F3"], fallback);

                // null 语义：Name == null 应为空集而不报错。
                Assert.Empty(db.Query(x => x.Name == null));
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 EFCore_SplitTable_PredicateQuery_FiltersServerSide；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_SplitTable_PredicateQuery_FiltersServerSide()
        {
            // P6-1：EFCore 分表条件查询逐表服务端过滤。
            var manager = new SQLManager();
            var db = manager.Create<LiteEfSplitItem>(ParamFor<LiteEfSplitItem>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new LiteEfSplitItem { Name = "E1", Age = 21, CreateTime = today });
                db.Add(new LiteEfSplitItem { Name = "E2", Age = 35, CreateTime = today.AddDays(-1) });

                var hit = db.Query(x => x.Age > 30).ToList();
                Assert.Single(hit);
                Assert.Equal("E2", hit[0].Name);
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 SingleEntity_Update_RoundTrips_AllTools；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、First；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SingleEntity_Update_RoundTrips_AllTools()
        {
            // P9-3：单实体按主键更新，三工具语义对齐（改名+年龄，读回验证；同步/异步各一）。
            var manager = new SQLManager();

            var dapper = manager.Create<LiteDapperItem>(ParamFor<LiteDapperItem>(ESQLToolType.Dapper));
            dapper.Connect();
            try
            {
                dapper.Add(new LiteDapperItem { Name = "U1", Age = 20 });
                var one = dapper.Query(x => x.Name == "U1").First();
                one.Age = 21;
                dapper.Update(one);
                Assert.Equal(21, dapper.Query(x => x.Name == "U1").First().Age);
            }
            finally { dapper.DisConnect(); }

            var ef = manager.Create<LiteEfItem>(ParamFor<LiteEfItem>(ESQLToolType.EFCore));
            ef.Connect();
            try
            {
                ef.Add(new LiteEfItem { Name = "U2", Age = 30 });
                var one = ef.Query(x => x.Name == "U2").First();
                // EF 先 Detach 查询态再整行更新（Query 用 AsNoTracking，无跟踪冲突）。
                one.Age = 31;
                ef.Update(one);
                Assert.Equal(31, ef.Query(x => x.Name == "U2").First().Age);
            }
            finally { ef.DisConnect(); }

            var sugar = manager.Create<LiteSugarItem>(ParamFor<LiteSugarItem>(ESQLToolType.SqlSugar));
            sugar.Connect();
            try
            {
                sugar.Add(new LiteSugarItem { Name = "U3", Age = 40 });
                var one = (await sugar.QueryAsync(x => x.Name == "U3")).First();
                one.Age = 41;
                await sugar.UpdateAsync(one);
                Assert.Equal(41, sugar.Query(x => x.Name == "U3").First().Age);
            }
            finally { sugar.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Transaction_CommitAndRollback_AllTools；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、ExecuteInTransaction、Add；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Transaction_CommitAndRollback_AllTools()
        {
            // P10：提交落盘、异常回滚（Dapper/EFCore/SqlSugar 非分表）。
            var manager = new SQLManager();

            var dapper = manager.Create<LiteDapperItem>(ParamFor<LiteDapperItem>(ESQLToolType.Dapper));
            dapper.Connect();
            try
            {
                dapper.ExecuteInTransaction(() =>
                {
                    dapper.Add(new LiteDapperItem { Name = "T1", Age = 1 });
                    dapper.Add(new LiteDapperItem { Name = "T2", Age = 2 });
                });
                Assert.Equal(2, dapper.Query().Count());

                Assert.Throws<InvalidOperationException>(() => dapper.ExecuteInTransaction(() =>
                {
                    dapper.Add(new LiteDapperItem { Name = "T3", Age = 3 });
                    throw new InvalidOperationException("boom");
                }));
                Assert.DoesNotContain(dapper.Query().ToList(), x => x.Name == "T3");

                // P11：异步事务同等语义。
                await dapper.ExecuteInTransactionAsync(async () =>
                {
                    await dapper.AddAsync(new LiteDapperItem { Name = "T4", Age = 4 });
                });
                Assert.Contains(dapper.Query().ToList(), x => x.Name == "T4");
                await Assert.ThrowsAsync<InvalidOperationException>(() => dapper.ExecuteInTransactionAsync(async () =>
                {
                    await dapper.AddAsync(new LiteDapperItem { Name = "T5", Age = 5 });
                    throw new InvalidOperationException("boom");
                }));
                Assert.DoesNotContain(dapper.Query().ToList(), x => x.Name == "T5");
            }
            finally { dapper.DisConnect(); }

            var ef = manager.Create<LiteEfItem>(ParamFor<LiteEfItem>(ESQLToolType.EFCore));
            ef.Connect();
            try
            {
                // P11：同步事务同等语义。
                ef.ExecuteInTransaction(() =>
                {
                    ef.Add(new LiteEfItem { Name = "E0", Age = 0 });
                });
                Assert.Contains(ef.Query().ToList(), x => x.Name == "E0");

                await ef.ExecuteInTransactionAsync(async () =>
                {
                    await ef.AddAsync(new LiteEfItem { Name = "E1", Age = 1 });
                    await ef.AddAsync(new LiteEfItem { Name = "E2", Age = 2 });
                });
                Assert.Equal(3, (await ef.QueryAsync()).Count());

                await Assert.ThrowsAsync<InvalidOperationException>(() => ef.ExecuteInTransactionAsync(async () =>
                {
                    await ef.AddAsync(new LiteEfItem { Name = "E3", Age = 3 });
                    throw new InvalidOperationException("boom");
                }));
                Assert.DoesNotContain((await ef.QueryAsync()).ToList(), x => x.Name == "E3");
            }
            finally { ef.DisConnect(); }

            var sugar = manager.Create<LiteSugarItem>(ParamFor<LiteSugarItem>(ESQLToolType.SqlSugar));
            sugar.Connect();
            try
            {
                // SqlSugar 在 SQLite 下无自增（IsIdentity 不支持），主键显式赋值。
                sugar.ExecuteInTransaction(() =>
                {
                    sugar.Add(new LiteSugarItem { Id = 101, Name = "S1", Age = 1 });
                    sugar.Add(new LiteSugarItem { Id = 102, Name = "S2", Age = 2 });
                });
                Assert.Equal(2, sugar.Query().Count());

                Assert.Throws<InvalidOperationException>(() => sugar.ExecuteInTransaction(() =>
                {
                    sugar.Add(new LiteSugarItem { Id = 103, Name = "S3", Age = 3 });
                    throw new InvalidOperationException("boom");
                }));
                Assert.DoesNotContain(sugar.Query().ToList(), x => x.Name == "S3");

                // P11：异步事务同等语义。
                await sugar.ExecuteInTransactionAsync(async () =>
                {
                    await sugar.AddAsync(new LiteSugarItem { Id = 104, Name = "S4", Age = 4 });
                });
                Assert.Contains(sugar.Query().ToList(), x => x.Name == "S4");
            }
            finally { sugar.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Transaction_Nested_Throws；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Throws、ExecuteInTransaction；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Transaction_Nested_Throws()
        {
            // P10：嵌套事务直接抛（不支持保存点语义）。
            var manager = new SQLManager();
            var db = manager.Create<LiteDapperItem>(ParamFor<LiteDapperItem>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                Assert.Throws<InvalidOperationException>(() => db.ExecuteInTransaction(() =>
                    db.ExecuteInTransaction(() => db.Add(new LiteDapperItem { Name = "N1", Age = 1 }))));
                Assert.Empty(db.Query());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Transaction_DapperSplit_IsAtomicAcrossTables；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Throws、ExecuteInTransaction；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Transaction_DapperSplit_IsAtomicAcrossTables()
        {
            // P10：Dapper 分表同连接原子（跨两张表写后抛，两表皆回滚）。
            var manager = new SQLManager();
            var db = (DapperBase<LiteDapperFilterItem>)manager.Create<LiteDapperFilterItem>(ParamFor<LiteDapperFilterItem>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                Assert.Throws<InvalidOperationException>(() => db.ExecuteInTransaction(() =>
                {
                    db.Add(new LiteDapperFilterItem { Name = "X1", Age = 1, CreateTime = today });
                    db.Add(new LiteDapperFilterItem { Name = "X2", Age = 2, CreateTime = today.AddDays(-1) });
                    throw new InvalidOperationException("boom");
                }));
                Assert.Empty(db.Query());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Transaction_EFCoreSplit_ThrowsNotSupported；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Throws、ExecuteInTransaction；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Transaction_EFCoreSplit_ThrowsNotSupported()
        {
            // P10：EFCore 分表 fork 独立连接， fail-fast 而非静默非原子。
            var manager = new SQLManager();
            var db = manager.Create<LiteEfSplitItem>(ParamFor<LiteEfSplitItem>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                Assert.Throws<NotSupportedException>(() => db.ExecuteInTransaction(() => { }));
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Dapper_MonthSplitTable_RoutesAndPrunes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、GetSplitTables；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_MonthSplitTable_RoutesAndPrunes()
        {
            // P9-2：Month 周期端到端（路由 + 月裁剪），跨月边界用显式日期。
            var manager = new SQLManager();
            var db = (DapperBase<LiteMonthItem>)manager.Create<LiteMonthItem>(ParamFor<LiteMonthItem>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                db.Add(new LiteMonthItem { Name = "M8", CreateTime = new DateTime(2026, 8, 15) });
                db.Add(new LiteMonthItem { Name = "M9", CreateTime = new DateTime(2026, 9, 10) });

                var tables = db.GetSplitTables();
                Assert.True(tables.Count >= 2, $"期望至少2张分表，实际: {string.Join(",", tables)}");

                var hit = db.Query(x => x.CreateTime >= new DateTime(2026, 9, 1) && x.CreateTime <= new DateTime(2026, 9, 30))
                    .Select(x => x.Name).ToList();
                Assert.Equal(["M9"], hit);
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// LiteMonthItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(DapperSplitType.Month)]
        public class LiteMonthItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// 测试相关入口 Dapper_SplitTable_DateEquality_PruneToHitTable；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ParamFor、Create、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_SplitTable_DateEquality_PruneToHitTable()
        {
            // P7-1：分表字段等值约束只查命中表；无约束查全部；矛盾日期给空集。
            var manager = new SQLManager();
            var param = ParamFor<LiteDapperFilterItem>(ESQLToolType.Dapper);
            var db = (DapperBase<LiteDapperFilterItem>)manager.Create<LiteDapperFilterItem>(param);
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new LiteDapperFilterItem { Name = "P1", Age = 21, CreateTime = today });
                db.Add(new LiteDapperFilterItem { Name = "P2", Age = 35, CreateTime = today.AddDays(-1) });
                Assert.True(db.GetSplitTables().Count >= 2);

                var router = new DapperSplitRouter<LiteDapperFilterItem>(param);

                // 等值裁剪到 1 张表，且端到端结果正确。
                Assert.Single(router.TablesFor(x => x.CreateTime == today));
                var hit = db.Query(x => x.CreateTime == today).Select(x => x.Name).ToList();
                Assert.Equal(["P1"], hit);

                // 非日期谓词不裁剪。
                Assert.Equal(db.GetSplitTables().Count, router.TablesFor(x => x.Age > 0).Count);

                // 矛盾日期 → 空集（AND 语义无解），不报错。
                Assert.Empty(router.TablesFor(x => x.CreateTime == today && x.CreateTime == today.AddDays(-1)));
                Assert.Empty(db.Query(x => x.CreateTime == today && x.CreateTime == today.AddDays(-1)));
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 Dapper_SplitTable_DateRange_PrunesAndStaysCorrect；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ParamFor、Create、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_SplitTable_DateRange_PrunesAndStaysCorrect()
        {
            // P8：有界范围只查覆盖表；结果与全表一致。
            var manager = new SQLManager();
            var param = ParamFor<LiteDapperFilterItem>(ESQLToolType.Dapper);
            var db = (DapperBase<LiteDapperFilterItem>)manager.Create<LiteDapperFilterItem>(param);
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new LiteDapperFilterItem { Name = "R0", Age = 20, CreateTime = today.AddDays(-2) });
                db.Add(new LiteDapperFilterItem { Name = "R1", Age = 21, CreateTime = today.AddDays(-1) });
                db.Add(new LiteDapperFilterItem { Name = "R2", Age = 22, CreateTime = today });
                Assert.True(db.GetSplitTables().Count >= 3);

                var router = new DapperSplitRouter<LiteDapperFilterItem>(param);

                var from = today.AddDays(-1);
                Assert.Equal(2, router.TablesFor(x => x.CreateTime >= from && x.CreateTime <= today).Count);

                var hit = db.Query(x => x.CreateTime >= from && x.CreateTime <= today)
                    .Select(x => x.Name).Order().ToList();
                Assert.Equal(["R1", "R2"], hit);

                // 半边无界不裁剪，但结果正确。
                var all = db.Query(x => x.CreateTime >= from).Select(x => x.Name).Order().ToList();
                Assert.Equal(["R1", "R2"], all);
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_SplitTable_AutoRoutesAndLists；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_SplitTable_AutoRoutesAndLists()
        {
            // P9-1：SqlSugar 原生分表自动路由 + GetSplitTables 列表。
            var manager = new SQLManager();
            var db = (SqlSugarBase<LiteSugarSplitItem>)manager.Create<LiteSugarSplitItem>(ParamFor<LiteSugarSplitItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new LiteSugarSplitItem { Id = 1, Name = "S0", CreateTime = today });
                db.Add(new LiteSugarSplitItem { Id = 2, Name = "S1", CreateTime = today.AddDays(-1) });

                var tables = db.GetSplitTables();
                Assert.True(tables.Count >= 2, $"期望至少2张分表，实际: {string.Join(",", tables)}");

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "S0");
                Assert.Contains(all, x => x.Name == "S1");
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 EFCore_MonthSplitTable_RoutesAndPrunes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、ToList；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_MonthSplitTable_RoutesAndPrunes()
        {
            // P13：EF Month 周期（路由 + 月裁剪，注意 EF 表名保留大小写）。
            var manager = new SQLManager();
            var db = manager.Create<LiteEfMonthItem>(ParamFor<LiteEfMonthItem>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                db.Add(new LiteEfMonthItem { Name = "EM8", CreateTime = new DateTime(2026, 8, 15) });
                db.Add(new LiteEfMonthItem { Name = "EM9", CreateTime = new DateTime(2026, 9, 10) });

                var hit = db.Query(x => x.CreateTime >= new DateTime(2026, 9, 1) && x.CreateTime <= new DateTime(2026, 9, 30))
                    .Select(x => x.Name).ToList();
                Assert.Equal(["EM9"], hit);
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_NonSplit_GetSplitTables_ReturnsEmpty；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Empty、GetSplitTables；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_NonSplit_GetSplitTables_ReturnsEmpty()
        {
            // P13：非分表实体取分表列表为空（契约锁定）。
            var manager = new SQLManager();
            var db = (SqlSugarBase<LiteSugarItem>)manager.Create<LiteSugarItem>(ParamFor<LiteSugarItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                Assert.Empty(db.GetSplitTables());
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 EFCore_SplitTable_DateRange_PrunesAndStaysCorrect；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_SplitTable_DateRange_PrunesAndStaysCorrect()
        {
            // P8：EFCore 同等裁剪（fork 精确匹配），结果正确。
            var manager = new SQLManager();
            var db = manager.Create<LiteEfSplitItem>(ParamFor<LiteEfSplitItem>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new LiteEfSplitItem { Name = "G0", Age = 20, CreateTime = today.AddDays(-2) });
                db.Add(new LiteEfSplitItem { Name = "G1", Age = 21, CreateTime = today.AddDays(-1) });
                db.Add(new LiteEfSplitItem { Name = "G2", Age = 22, CreateTime = today });

                var from = today.AddDays(-1);
                var hit = db.Query(x => x.CreateTime >= from && x.CreateTime <= today)
                    .Select(x => x.Name).Order().ToList();
                Assert.Equal(["G1", "G2"], hit);
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 EFCore_CRUD_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<LiteEfItem>(ParamFor<LiteEfItem>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new LiteEfItem { Name = "E1", Age = 31 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "E1");

                db.Update(x => x.Name == "E1", new LiteEfItem { Name = "E1", Age = 32 });
                Assert.Equal(32, db.Query(x => x.Name == "E1").First().Age);

                db.Delete(x => x.Name == "E1");
                Assert.Empty(db.Query(x => x.Name == "E1"));
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_CRUD_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_CRUD_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<LiteSugarItem>(ParamFor<LiteSugarItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new LiteSugarItem { Name = "S1", Age = 41 });

                Assert.Contains(db.Query().ToList(), x => x.Name == "S1");

                db.Update(x => x.Name == "S1", new LiteSugarItem { Name = "S1", Age = 42 });
                Assert.Equal(42, db.Query(x => x.Name == "S1").First().Age);

                db.Delete(x => x.Name == "S1");
                Assert.Empty(db.Query(x => x.Name == "S1"));
            }
            finally
            {
                db.DisConnect();
            }
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Exists、Delete、SuppressFinalize；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public void Dispose()
        {
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
