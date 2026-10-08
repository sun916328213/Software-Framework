// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqliteP0Tests、P0Item、P0SugarItem、P0DapperDay、P0EfDay、P0EfYear、P0DapperYear、P0SugarDay、P0SugarYear。成员说明可结合下方具体实现阅读。
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
    /// SQLite 真库 P0 缺口测试：G-2 维护 API / G-3 AddRange 分组 / G-4 Year 分表 /
    /// G-5 事务错误路径 / G-6 异步碎片。
    /// </summary>
    public class SqliteP0Tests : IDisposable
    {
        /// <summary>
        /// P0Item 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class P0Item
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// P0SugarItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class P0SugarItem
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// P0DapperDay 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(DapperSplitType.Day)]
        public class P0DapperDay
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// P0EfDay 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Day)]
        public class P0EfDay
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// P0EfYear 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Year)]
        public class P0EfYear
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// P0DapperYear 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DapperSplitTable(DapperSplitType.Year)]
        public class P0DapperYear
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// P0SugarDay 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [SugarTable("p0sugarday_{year}{month}{day}")]
        [SplitTable(SugarSplitType.Day)]
        public class P0SugarDay
        {
            [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            [SplitField]
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// P0SugarYear 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [SugarTable("p0sugaryear_{year}{month}{day}")]
        [SplitTable(SugarSplitType.Year)]
        public class P0SugarYear
        {
            [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            [SplitField]
            public DateTime CreateTime { get; set; }
        }

        private readonly string _file = Path.Combine(
            Path.GetTempPath(), $"dm_sqlite_p0_{Guid.NewGuid():N}.db");

        /// <summary>
        /// 测试相关入口 ParamFor；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewGuid；具体条件和先后次序见方法体。
        /// </summary>
        private SQLParam ParamFor<T>(ESQLToolType tool) => new()
        {
            Name = $"p0-{typeof(T).Name}-{tool}-{Guid.NewGuid():N}",
            FilePath = _file,
            DBName = _file,
            DBType = ESQLType.SQLite,
            ToolType = tool,
        };

        #region G-2 SqlSugar 维护 API

        [Fact]
        public async Task SqlSugar_TableExists_ReflectsCreateAndDrop()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarItem>)manager.Create<P0SugarItem>(
                ParamFor<P0SugarItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                Assert.True(db.TableExists());
                Assert.True(await db.TableExistsAsync());

                var tableName = "p0_sugar_tmp_exists";
                Assert.False(db.TableExists(tableName));
                Assert.False(await db.TableExistsAsync(tableName));
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_DropTable_RemovesTable_And_NoOpWhenMissing；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、CreateTable、True；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SqlSugar_DropTable_RemovesTable_And_NoOpWhenMissing()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarItem>)manager.Create<P0SugarItem>(
                ParamFor<P0SugarItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                var defaultTable = "p0sugaritem";
                db.CreateTable();
                Assert.True(db.TableExists(defaultTable));

                await db.DropTableAsync(defaultTable);
                Assert.False(db.TableExists(defaultTable));

                // 不存在时静默 no-op
                db.DropTable("p0_sugar_never_existed");
                await db.DropTableAsync("p0_sugar_never_existed");

                // 重建以便 Dispose 清理路径正常
                db.CreateTable();
                Assert.True(db.TableExists());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_DropAllTables_ClearsAll；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、CreateTable、Add；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SqlSugar_DropAllTables_ClearsAll()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarItem>)manager.Create<P0SugarItem>(
                ParamFor<P0SugarItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                db.CreateTable();
                db.Add(new P0SugarItem { Id = 1, Name = "D1", Age = 1 });
                Assert.Single(db.Query());

                await db.DropAllTablesAsync();
                Assert.False(db.TableExists());

                // DropAll 后可重建并继续写入
                db.CreateTable();
                db.Add(new P0SugarItem { Id = 2, Name = "D2", Age = 2 });
                Assert.Single(db.Query());
            }
            finally { db.DisConnect(); }
        }

        #endregion

        #region G-3 AddRange / AddRangeAsync 分表分组

        [Fact]
        public void EFCore_AddRange_GroupsBySplitTable()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0EfDay>(ParamFor<P0EfDay>(ESQLToolType.EFCore));
            var split = (DataManager.Runtime.SQL.EFCore.EFCoreBase<P0EfDay>)db;
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.AddRange(new[]
                {
                    new P0EfDay { Name = "AR0", CreateTime = today },
                    new P0EfDay { Name = "AR1", CreateTime = today },
                    new P0EfDay { Name = "AR2", CreateTime = today.AddDays(-1) },
                });

                var tables = split.GetSplitTables();
                Assert.True(tables.Count >= 2, $"期望至少2张分表，实际: {string.Join(",", tables)}");

                var all = db.Query().ToList();
                Assert.Equal(3, all.Count);
                Assert.Contains(all, x => x.Name == "AR0");
                Assert.Contains(all, x => x.Name == "AR2");
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_AddRangeAsync_GroupsBySplitTable；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、AddRangeAsync、AddDays；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task EFCore_AddRangeAsync_GroupsBySplitTable()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0EfDay>(ParamFor<P0EfDay>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                await db.AddRangeAsync(new[]
                {
                    new P0EfDay { Name = "A0", CreateTime = today },
                    new P0EfDay { Name = "A1", CreateTime = today.AddDays(-1) },
                    new P0EfDay { Name = "A2", CreateTime = today.AddDays(-1) },
                });

                var all = (await db.QueryAsync()).ToList();
                Assert.Equal(3, all.Count);
                Assert.True(((DataManager.Runtime.SQL.EFCore.EFCoreBase<P0EfDay>)db).GetSplitTables().Count >= 2);
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Dapper_AddRange_And_AddRangeAsync_RouteSplitRows；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、AddRange、AddDays；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Dapper_AddRange_And_AddRangeAsync_RouteSplitRows()
        {
            var manager = new SQLManager();
            var db = (DapperBase<P0DapperDay>)manager.Create<P0DapperDay>(
                ParamFor<P0DapperDay>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.AddRange(new[]
                {
                    new P0DapperDay { Name = "DR0", CreateTime = today },
                    new P0DapperDay { Name = "DR1", CreateTime = today.AddDays(-1) },
                });
                Assert.Equal(2, db.Query().Count());

                await db.AddRangeAsync(new[]
                {
                    new P0DapperDay { Name = "DR2", CreateTime = today },
                    new P0DapperDay { Name = "DR3", CreateTime = today.AddDays(-2) },
                });

                var all = db.Query().ToList();
                Assert.Equal(4, all.Count);
                Assert.True(db.GetSplitTables().Count >= 3);
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_AddRange_And_AddRangeAsync_RouteSplitRows；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、AddRange、AddDays；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SqlSugar_AddRange_And_AddRangeAsync_RouteSplitRows()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarDay>)manager.Create<P0SugarDay>(
                ParamFor<P0SugarDay>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.AddRange(new[]
                {
                    new P0SugarDay { Id = 1, Name = "SR0", CreateTime = today },
                    new P0SugarDay { Id = 2, Name = "SR1", CreateTime = today.AddDays(-1) },
                });
                Assert.Equal(2, db.Query().Count());

                await db.AddRangeAsync(new[]
                {
                    new P0SugarDay { Id = 3, Name = "SR2", CreateTime = today },
                });

                var all = db.Query().ToList();
                Assert.Equal(3, all.Count);
                Assert.True(db.GetSplitTables().Count >= 2);
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_AddRange_NonSplit_WritesSingleTable；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、AddRange；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_AddRange_NonSplit_WritesSingleTable()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0Item>(ParamFor<P0Item>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.AddRange(new[]
                {
                    new P0Item { Name = "N0", Age = 1 },
                    new P0Item { Name = "N1", Age = 2 },
                });
                Assert.Equal(2, db.Query().Count());
            }
            finally { db.DisConnect(); }
        }

        #endregion

        #region G-4 SplitPeriod.Year 分表端到端

        [Fact]
        public void EFCore_YearSplit_RoutesAndPrunes()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0EfYear>(ParamFor<P0EfYear>(ESQLToolType.EFCore));
            var split = (DataManager.Runtime.SQL.EFCore.EFCoreBase<P0EfYear>)db;
            db.Connect();
            try
            {
                db.Add(new P0EfYear { Name = "Y2024", CreateTime = new DateTime(2024, 6, 1) });
                db.Add(new P0EfYear { Name = "Y2025", CreateTime = new DateTime(2025, 3, 15) });
                db.Add(new P0EfYear { Name = "Y2026", CreateTime = new DateTime(2026, 9, 23) });

                var tables = split.GetSplitTables();
                Assert.True(tables.Count >= 3, $"期望至少3张年表，实际: {string.Join(",", tables)}");

                var hit = db.Query(x => x.CreateTime >= new DateTime(2025, 1, 1)
                                     && x.CreateTime <= new DateTime(2025, 12, 31))
                    .Select(x => x.Name).ToList();
                Assert.Equal(["Y2025"], hit);

                Assert.Equal(3, db.Query().Count());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Dapper_YearSplit_RoutesAndPrunes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、GetSplitTables；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_YearSplit_RoutesAndPrunes()
        {
            var manager = new SQLManager();
            var db = (DapperBase<P0DapperYear>)manager.Create<P0DapperYear>(
                ParamFor<P0DapperYear>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                db.Add(new P0DapperYear { Name = "DY2024", CreateTime = new DateTime(2024, 1, 1) });
                db.Add(new P0DapperYear { Name = "DY2026", CreateTime = new DateTime(2026, 1, 1) });

                var tables = db.GetSplitTables();
                Assert.True(tables.Count >= 2, $"期望至少2张年表，实际: {string.Join(",", tables)}");

                var hit = db.Query(x => x.CreateTime >= new DateTime(2026, 1, 1)
                                     && x.CreateTime <= new DateTime(2026, 12, 31))
                    .Select(x => x.Name).ToList();
                Assert.Equal(["DY2026"], hit);
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_YearSplit_RoutesAndLists；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、GetSplitTables；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_YearSplit_RoutesAndLists()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarYear>)manager.Create<P0SugarYear>(
                ParamFor<P0SugarYear>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                db.Add(new P0SugarYear { Id = 1, Name = "SY2025", CreateTime = new DateTime(2025, 5, 1) });
                db.Add(new P0SugarYear { Id = 2, Name = "SY2026", CreateTime = new DateTime(2026, 5, 1) });

                var tables = db.GetSplitTables();
                Assert.True(tables.Count >= 2, $"期望至少2张年表，实际: {string.Join(",", tables)}");

                var all = db.Query().ToList();
                Assert.Contains(all, x => x.Name == "SY2025");
                Assert.Contains(all, x => x.Name == "SY2026");
            }
            finally { db.DisConnect(); }
        }

        #endregion

        #region G-5 事务错误路径

        [Fact]
        public void EFCore_NestedTransaction_Throws()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0Item>(ParamFor<P0Item>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                Assert.Throws<InvalidOperationException>(() => db.ExecuteInTransaction(() =>
                    db.ExecuteInTransaction(() => db.Add(new P0Item { Name = "N1", Age = 1 }))));
                Assert.Empty(db.Query());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_NestedTransactionAsync_Throws；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、ThrowsAsync、ExecuteInTransactionAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task EFCore_NestedTransactionAsync_Throws()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0Item>(ParamFor<P0Item>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await db.ExecuteInTransactionAsync(async () =>
                        await db.ExecuteInTransactionAsync(async () =>
                            await db.AddAsync(new P0Item { Name = "NA", Age = 1 })));
                });
                Assert.Empty(await db.QueryAsync());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_SplitTransactionAsync_ThrowsNotSupported；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、ThrowsAsync、ExecuteInTransactionAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task EFCore_SplitTransactionAsync_ThrowsNotSupported()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0EfDay>(ParamFor<P0EfDay>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                await Assert.ThrowsAsync<NotSupportedException>(() =>
                    db.ExecuteInTransactionAsync(() => Task.CompletedTask));
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_NestedTransaction_Throws；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Throws、ExecuteInTransaction；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_NestedTransaction_Throws()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarItem>)manager.Create<P0SugarItem>(
                ParamFor<P0SugarItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                Assert.Throws<InvalidOperationException>(() => db.ExecuteInTransaction(() =>
                    db.ExecuteInTransaction(() =>
                        db.Add(new P0SugarItem { Id = 1, Name = "N1", Age = 1 }))));
                Assert.Empty(db.Query());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_NestedTransactionAsync_Throws；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、ThrowsAsync、ExecuteInTransactionAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SqlSugar_NestedTransactionAsync_Throws()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarItem>)manager.Create<P0SugarItem>(
                ParamFor<P0SugarItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    db.ExecuteInTransactionAsync(async () =>
                        await db.ExecuteInTransactionAsync(async () =>
                            await db.AddAsync(new P0SugarItem { Id = 2, Name = "NA", Age = 1 }))));
                Assert.Empty(db.Query());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_SplitTransaction_IsAtomicAcrossTables；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、ExecuteInTransaction、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_SplitTransaction_IsAtomicAcrossTables()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarYear>)manager.Create<P0SugarYear>(
                ParamFor<P0SugarYear>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                // 先提交跨年两行，确保两张分表已存在（SQLite DDL 随事务回滚，不能在 rollback 后再断言表存在）。
                db.ExecuteInTransaction(() =>
                {
                    db.Add(new P0SugarYear { Id = 1, Name = "P2024", CreateTime = new DateTime(2024, 1, 1) });
                    db.Add(new P0SugarYear { Id = 2, Name = "P2025", CreateTime = new DateTime(2025, 1, 1) });
                });
                Assert.Equal(2, db.Query().Count());
                Assert.True(db.GetSplitTables().Count >= 2);

                Assert.Throws<InvalidOperationException>(() => db.ExecuteInTransaction(() =>
                {
                    db.Add(new P0SugarYear { Id = 3, Name = "TX2024", CreateTime = new DateTime(2024, 6, 1) });
                    db.Add(new P0SugarYear { Id = 4, Name = "TX2025", CreateTime = new DateTime(2025, 6, 1) });
                    throw new InvalidOperationException("boom");
                }));

                // 回滚后只剩先提交的两行，跨年插入均未落盘。
                var rest = db.Query().Select(x => x.Name).Order().ToList();
                Assert.Equal(["P2024", "P2025"], rest);
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_SplitTransaction_Commit_PersistsAcrossTables；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、ExecuteInTransaction、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_SplitTransaction_Commit_PersistsAcrossTables()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarYear>)manager.Create<P0SugarYear>(
                ParamFor<P0SugarYear>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                db.ExecuteInTransaction(() =>
                {
                    db.Add(new P0SugarYear { Id = 11, Name = "CY2024", CreateTime = new DateTime(2024, 1, 1) });
                    db.Add(new P0SugarYear { Id = 12, Name = "CY2025", CreateTime = new DateTime(2025, 1, 1) });
                });
                Assert.Equal(2, db.Query().Count());
            }
            finally { db.DisConnect(); }
        }

        #endregion

        #region G-6 异步 API 碎片

        [Fact]
        public async Task Dapper_UpdateAsync_And_QueryAsync_Predicate()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0Item>(ParamFor<P0Item>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new P0Item { Name = "U0", Age = 20 });

                var one = (await db.QueryAsync(x => x.Name == "U0")).First();
                one.Age = 21;
                await db.UpdateAsync(one);
                Assert.Equal(21, (await db.QueryAsync(x => x.Name == "U0")).First().Age);

                await db.UpdateAsync(x => x.Name == "U0", new P0Item { Name = "U0", Age = 22 });
                Assert.Equal(22, (await db.QueryAsync(x => x.Name == "U0")).First().Age);
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_DeleteAsync_DeleteAllAsync_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、AddRangeAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task EFCore_DeleteAsync_DeleteAllAsync_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0Item>(ParamFor<P0Item>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                db.DeleteAll();
                await db.AddRangeAsync(new[]
                {
                    new P0Item { Name = "E0", Age = 1 },
                    new P0Item { Name = "E1", Age = 2 },
                });
                Assert.Equal(2, (await db.QueryAsync()).Count());

                await db.DeleteAsync(x => x.Name == "E0");
                Assert.DoesNotContain((await db.QueryAsync()).ToList(), x => x.Name == "E0");

                await db.DeleteAllAsync();
                Assert.Empty(await db.QueryAsync());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_Split_DeleteAsync_And_DeleteAllAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task EFCore_Split_DeleteAsync_And_DeleteAllAsync()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0EfDay>(ParamFor<P0EfDay>(ESQLToolType.EFCore));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new P0EfDay { Name = "DS0", CreateTime = today });
                db.Add(new P0EfDay { Name = "DS1", CreateTime = today.AddDays(-1) });

                await db.DeleteAsync(x => x.Name == "DS0");
                var rest = (await db.QueryAsync()).ToList();
                Assert.DoesNotContain(rest, x => x.Name == "DS0");
                Assert.Contains(rest, x => x.Name == "DS1");

                await db.DeleteAllAsync();
                Assert.Empty(await db.QueryAsync());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_DeleteAsync_DeleteAllAsync_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、AddRangeAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SqlSugar_DeleteAsync_DeleteAllAsync_RoundTrip()
        {
            var manager = new SQLManager();
            var db = manager.Create<P0SugarItem>(ParamFor<P0SugarItem>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                db.DeleteAll();
                await db.AddRangeAsync(new[]
                {
                    new P0SugarItem { Id = 1, Name = "S0", Age = 1 },
                    new P0SugarItem { Id = 2, Name = "S1", Age = 2 },
                });
                Assert.Equal(2, (await db.QueryAsync()).Count());

                await db.DeleteAsync(x => x.Name == "S0");
                Assert.DoesNotContain((await db.QueryAsync()).ToList(), x => x.Name == "S0");

                await db.DeleteAllAsync();
                Assert.Empty(await db.QueryAsync());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_Split_QueryAsync_DeleteAsync_CoverAllTables；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SqlSugar_Split_QueryAsync_DeleteAsync_CoverAllTables()
        {
            var manager = new SQLManager();
            var db = (SqlSugarBase<P0SugarDay>)manager.Create<P0SugarDay>(
                ParamFor<P0SugarDay>(ESQLToolType.SqlSugar));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new P0SugarDay { Id = 1, Name = "SQ0", CreateTime = today });
                db.Add(new P0SugarDay { Id = 2, Name = "SQ1", CreateTime = today.AddDays(-1) });
                Assert.True(db.GetSplitTables().Count >= 2);

                await db.DeleteAsync(x => x.Name == "SQ0");
                var rest = (await db.QueryAsync()).ToList();
                Assert.DoesNotContain(rest, x => x.Name == "SQ0");
                Assert.Contains(rest, x => x.Name == "SQ1");

                await db.DeleteAllAsync();
                Assert.Empty(await db.QueryAsync());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Dapper_Split_UpdateAsync_Predicate_Routes；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Add、AddDays；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Dapper_Split_UpdateAsync_Predicate_Routes()
        {
            var manager = new SQLManager();
            var db = (DapperBase<P0DapperDay>)manager.Create<P0DapperDay>(
                ParamFor<P0DapperDay>(ESQLToolType.Dapper));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new P0DapperDay { Name = "UP0", CreateTime = today });
                db.Add(new P0DapperDay { Name = "UP1", CreateTime = today.AddDays(-1) });

                await db.UpdateAsync(x => x.Name == "UP0", new P0DapperDay { Name = "UP0", CreateTime = today });

                var hit = (await db.QueryAsync(x => x.Name == "UP0")).ToList();
                Assert.Single(hit);
            }
            finally { db.DisConnect(); }
        }

        #endregion

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
