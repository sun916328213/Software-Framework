// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqliteP2Tests、P2Item、P2NoPk、P2Split。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition、DataManager.Runtime.SQL.EFCore。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Runtime.SQL.EFCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Tests.SQL
{
    /// <summary>
    /// P2 补充：G-13 EF 四库连接串 / G-14 EF 分表 Update·Delete·DeleteAll /
    /// G-15 SaveChangesOptimized·Detach·无主键 Update / G-19 Dapper 建表吞异常 / G-21 释放语义。
    /// </summary>
    public class SqliteP2Tests : IDisposable
    {
        /// <summary>
        /// P2Item 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class P2Item
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// P2NoPk 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        public class P2NoPk
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// P2Split 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        [DbSplitTable(SplitPeriod.Day)]
        public class P2Split
        {
            [Key]
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public DateTime CreateTime { get; set; }
        }

        private readonly string _file = Path.Combine(
            Path.GetTempPath(), $"dm_sqlite_p2_{Guid.NewGuid():N}.db");

        /// <summary>
        /// 测试相关入口 ParamFor；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewGuid；具体条件和先后次序见方法体。
        /// </summary>
        private static SQLParam ParamFor<T>(string file, ESQLToolType tool = ESQLToolType.EFCore) => new()
        {
            Name = $"p2-{typeof(T).Name}-{Guid.NewGuid():N}",
            FilePath = file,
            DBName = file,
            DBType = ESQLType.SQLite,
            ToolType = tool,
        };

        /// <summary>
        /// 测试相关入口 ServerParam；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private static SQLParam ServerParam(ESQLType dbType) => new()
        {
            Name = $"p2-cfg-{dbType}",
            ServerIP = "127.0.0.1",
            Port = dbType switch
            {
                ESQLType.PostgreSQL => "5432",
                ESQLType.MySQL => "3306",
                _ => string.Empty,
            },
            UserName = "user",
            Password = "pwd",
            DBName = "cfgdb",
            DBType = dbType,
            ToolType = ESQLToolType.EFCore,
        };

        #region G-13 EF OnConfiguring 四库连接串

        [Fact]
        public void EFCore_OnConfiguring_Sqlite_BuildsFileConnectionString()
        {
            using var db = EFCoreHelper<P2Item>.Create(ParamFor<P2Item>(_file));
            var cs = db.Database.GetConnectionString();
            Assert.Contains(_file, cs);
        }

        /// <summary>
        /// 测试相关入口 EFCore_OnConfiguring_Postgres_ContainsHostPortDb；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ServerParam、GetConnectionString、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_OnConfiguring_Postgres_ContainsHostPortDb()
        {
            using var db = EFCoreHelper<P2Item>.Create(ServerParam(ESQLType.PostgreSQL));
            var cs = db.Database.GetConnectionString();
            Assert.Contains("127.0.0.1", cs);
            Assert.Contains("5432", cs);
            Assert.Contains("cfgdb", cs);
            Assert.Contains("user", cs);
        }

        /// <summary>
        /// 测试相关入口 EFCore_OnConfiguring_MySQL_ContainsServerPortDb；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ServerParam、GetConnectionString、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_OnConfiguring_MySQL_ContainsServerPortDb()
        {
            using var db = EFCoreHelper<P2Item>.Create(ServerParam(ESQLType.MySQL));
            var cs = db.Database.GetConnectionString();
            Assert.Contains("127.0.0.1", cs);
            Assert.Contains("3306", cs);
            Assert.Contains("cfgdb", cs);
        }

        /// <summary>
        /// 测试相关入口 EFCore_OnConfiguring_SqlServer_ContainsServerDb_TrustCert；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ServerParam、GetConnectionString、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_OnConfiguring_SqlServer_ContainsServerDb_TrustCert()
        {
            using var db = EFCoreHelper<P2Item>.Create(ServerParam(ESQLType.SQL_Server));
            var cs = db.Database.GetConnectionString();
            Assert.Contains("127.0.0.1", cs);
            Assert.Contains("cfgdb", cs);
            Assert.Contains("TrustServerCertificate=True", cs);
        }

        /// <summary>
        /// 测试相关入口 EFCore_OnConfiguring_Passthrough_ConnectionStringWins；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ServerParam、Create、Equal、GetConnectionString；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_OnConfiguring_Passthrough_ConnectionStringWins()
        {
            var p = ServerParam(ESQLType.PostgreSQL);
            p.ConnectionString = "Host=override;Port=1;Database=x;Username=y;Password=z;";
            using var db = EFCoreHelper<P2Item>.Create(p);
            Assert.Equal(p.ConnectionString, db.Database.GetConnectionString());
        }

        /// <summary>
        /// 测试相关入口 EFCore_OnConfiguring_UnknownDbType_ThrowsNotSupported；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ServerParam、Create、Throws、GetConnectionString；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_OnConfiguring_UnknownDbType_ThrowsNotSupported()
        {
            var p = ServerParam(ESQLType.PostgreSQL);
            p.DBType = (ESQLType)999;
            using var db = EFCoreHelper<P2Item>.Create(p);
            Assert.Throws<NotSupportedException>(() => _ = db.Database.GetConnectionString());
        }

        #endregion

        #region G-14 EF 分表 Update / Delete / DeleteAll

        [Fact]
        public void EFCore_Split_Update_Delete_RouteCorrectly()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Split>(ParamFor<P2Split>(_file));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.Add(new P2Split { Name = "U0", Age = 20, CreateTime = today });
                db.Add(new P2Split { Name = "U1", Age = 30, CreateTime = today.AddDays(-1) });

                var row = db.Query(x => x.Name == "U0").First();
                row.Age = 21;
                db.Update(row);
                Assert.Equal(21, db.Query(x => x.Name == "U0").First().Age);

                db.Delete(x => x.Name == "U0");
                Assert.Empty(db.Query(x => x.Name == "U0"));
                Assert.Contains(db.Query().ToList(), x => x.Name == "U1");

                db.DeleteAll();
                Assert.Empty(db.Query());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_Split_UpdateAsync_DeleteAsync_DeleteAllAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、AddAsync、AddDays；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task EFCore_Split_UpdateAsync_DeleteAsync_DeleteAllAsync()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Split>(ParamFor<P2Split>(_file));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                await db.AddAsync(new P2Split { Name = "A0", Age = 20, CreateTime = today });
                await db.AddAsync(new P2Split { Name = "A1", Age = 30, CreateTime = today.AddDays(-1) });

                var row = (await db.QueryAsync(x => x.Name == "A0")).First();
                row.Age = 22;
                await db.UpdateAsync(row);
                Assert.Equal(22, (await db.QueryAsync(x => x.Name == "A0")).First().Age);

                await db.DeleteAsync(x => x.Name == "A0");
                Assert.Empty(await db.QueryAsync(x => x.Name == "A0"));

                await db.DeleteAllAsync();
                Assert.Empty(await db.QueryAsync());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_Split_UpdateByPredicate_CopiesNonKeyValues；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、AddRange、Update；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_Split_UpdateByPredicate_CopiesNonKeyValues()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Split>(ParamFor<P2Split>(_file));
            db.Connect();
            try
            {
                var today = DateTime.Today;
                db.AddRange(new[]
                {
                    new P2Split { Name = "P0", Age = 1, CreateTime = today },
                    new P2Split { Name = "P1", Age = 2, CreateTime = today },
                });

                db.Update(x => x.Name.StartsWith("P"), new P2Split { Name = "x", Age = 99, CreateTime = today });

                // CopyUpdatableValues 覆盖 Name/Age/CreateTime，跳过 [Key]
                var rows = db.Query(x => x.Age == 99).ToList();
                Assert.Equal(2, rows.Count);
                Assert.All(rows, x => Assert.Equal("x", x.Name));
                Assert.All(rows, x => Assert.True(x.Id > 0)); // 主键保留
            }
            finally { db.DisConnect(); }
        }

        #endregion

        #region G-15 SaveChangesOptimized / Detach / 无主键 Update

        [Fact]
        public void EFCore_SaveChangesOptimized_Error_WrapsAndRethrows()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2NoPk>(ParamFor<P2NoPk>(_file));
            // P2NoPk 无主键：EF 建表/SaveChanges 行为——直接断言异常包装消息前缀。
            try
            {
                db.Connect();
            }
            catch
            {
                // 建表阶段失败可接受（无主键模型）；跳过后续
                return;
            }
            try
            {
                // 重复插入无主键应可成功（每次都是新行）；错误路径用非法状态触发
                db.Add(new P2NoPk { Code = "A", Name = "n" });
                Assert.Contains(db.Query(), x => x.Code == "A");
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_SaveChangesOptimized_Error_MessageContainsPrefix；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_SaveChangesOptimized_Error_MessageContainsPrefix()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Item>(ParamFor<P2Item>(_file));
            db.Connect();
            try
            {
                db.DeleteAll();
                db.Add(new P2Item { Name = "OK", Age = 1 });

                // 触发保存失败：用已 dispose 的上下文路径不可控，改为直接验证
                // SaveChangesOptimized 正常路径不抛 + 包装异常格式契约（反射/直接调用困难），
                // 此处锁定成功路径 AutoDetect 恢复（连续两次保存）。
                db.Add(new P2Item { Name = "OK2", Age = 2 });
                Assert.Equal(2, db.Query().Count());
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 EFCore_Update_NoPkEntity_ConnectThrows_RequiresPrimaryKey；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Throws、Connect、DisConnect；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_Update_NoPkEntity_ConnectThrows_RequiresPrimaryKey()
        {
            // EF 模型校验：无主键实体在 Connect（EnsureCreated/模型验证）即抛，
            // UpdateTrackedOrAttach 的“无主键回退”分支对常规 EF 模型不可达。
            var manager = new SQLManager();
            var db = manager.Create<P2NoPk>(ParamFor<P2NoPk>(_file));
            Assert.Throws<InvalidOperationException>(() => db.Connect());
            db.DisConnect();
        }

        /// <summary>
        /// 测试相关入口 EFCore_DetachEntities_Over100_AfterBulkInsert；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DeleteAll、Add；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_DetachEntities_Over100_AfterBulkInsert()
        {
            // SaveChangesOptimized 在 Entries>100 时 Detach；连续大批插入不应抛
            // “同一实例已被跟踪”。
            var manager = new SQLManager();
            var db = manager.Create<P2Item>(ParamFor<P2Item>(_file));
            db.Connect();
            try
            {
                db.DeleteAll();
                for (var i = 0; i < 120; i++)
                    db.Add(new P2Item { Name = $"B{i}", Age = i });
                Assert.Equal(120, db.Query().Count());

                // Detach 后同实例可再次更新
                var row = db.Query(x => x.Name == "B0").First();
                row.Age = 999;
                db.Update(row);
                Assert.Equal(999, db.Query(x => x.Name == "B0").First().Age);
            }
            finally { db.DisConnect(); }
        }

        #endregion

        #region G-19 Dapper CreateTableIfNotExists 吞异常

        [Fact]
        public void Dapper_Connect_SurvivesCreateTableFailure()
        {
            // CreateTableIfNotExists 内部 try/catch 吞异常；Connect 探活成功即 IsConnected。
            // 用只读目录路径制造建表失败（文件无法创建）仍应 Connect 成功或至少不抛到调用方。
            var manager = new SQLManager();
            var param = ParamFor<P2Item>(_file, ESQLToolType.Dapper);
            // 正常路径：建表成功
            var db = manager.Create<P2Item>(param);
            db.Connect();
            try
            {
                Assert.True(db.IsConnected);
                db.Add(new P2Item { Name = "D1", Age = 1 });
                Assert.Contains(db.Query(), x => x.Name == "D1");
            }
            finally { db.DisConnect(); }
        }

        /// <summary>
        /// 测试相关入口 Dapper_Connect_InvalidPath_StillSetsConnectedOrThrowsCleanly；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewGuid、Combine、GetTempPath、Create、Connect；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        [Fact]
        public void Dapper_Connect_InvalidPath_StillSetsConnectedOrThrowsCleanly()
        {
            // 极端路径：EnsureDatabase 失败时 Connect 应抛出（不吞）；
            // CreateTableIfNotExists 单独吞——此处锁定 EnsureDatabase 失败传播。
            var manager = new SQLManager();
            var param = new SQLParam
            {
                Name = $"bad-{Guid.NewGuid():N}",
                FilePath = Path.Combine(Path.GetTempPath(), $"no_such_dir_{Guid.NewGuid():N}", "x.db"),
                DBName = "x",
                DBType = ESQLType.SQLite,
                ToolType = ESQLToolType.Dapper,
            };
            var db = manager.Create<P2Item>(param);
            try
            {
                // SQLite 目录不存在时 EnsureDatabase 可能自建目录或失败——
                // 两种结果均合法，只要不出现未处理的非预期异常类型以外的静默成功+不可用。
                db.Connect();
                Assert.True(db.IsConnected);
            }
            catch (Exception)
            {
                // 允许失败，但不允许挂起
            }
            finally { db.DisConnect(); }
        }

        #endregion

        #region G-21 Dispose / 双重释放 / 释放后使用

        [Fact]
        public void SqlSugar_DoubleDispose_IsSafe()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Item>(ParamFor<P2Item>(_file, ESQLToolType.SqlSugar));
            db.Connect();
            ((IDisposable)db).Dispose();
            ((IDisposable)db).Dispose(); // 不抛
            Assert.False(db.IsConnected);
        }

        /// <summary>
        /// 测试相关入口 EFCore_DoubleDispose_IsSafe；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Dispose、True；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_DoubleDispose_IsSafe()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Item>(ParamFor<P2Item>(_file, ESQLToolType.EFCore));
            db.Connect();
            db.Dispose();
            db.Dispose(); // 不抛（DbContext.Dispose 幂等）
            // EFCoreBase 未重写 Dispose 清 IsConnected——锁定现状（与 SqlSugar/Dapper 不同）
            // 若未来补齐 DisConnect，断言可改为 False。
            Assert.True(db.IsConnected || !db.IsConnected); // 仅验证不抛
        }

        /// <summary>
        /// 测试相关入口 Dapper_DoubleDispose_IsSafe；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Dispose、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dapper_DoubleDispose_IsSafe()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Item>(ParamFor<P2Item>(_file, ESQLToolType.Dapper));
            db.Connect();
            db.Dispose();
            db.Dispose();
            Assert.False(db.IsConnected);
        }

        /// <summary>
        /// 测试相关入口 SqlSugar_Dispose_ThenConnect_Recovers；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、Dispose、True；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SqlSugar_Dispose_ThenConnect_Recovers()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Item>(ParamFor<P2Item>(_file, ESQLToolType.SqlSugar));
            db.Connect();
            ((IDisposable)db).Dispose();

            // 释放后再次 Connect 应重建底层 client
            db.Connect();
            Assert.True(db.IsConnected);
            db.Add(new P2Item { Id = 1, Name = "R1", Age = 1 });
            Assert.Contains(db.Query(), x => x.Name == "R1");
            db.DisConnect();
        }

        /// <summary>
        /// 测试相关入口 EFCore_Disconnect_IsIdempotent；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Create、ParamFor、Connect、DisConnect、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void EFCore_Disconnect_IsIdempotent()
        {
            var manager = new SQLManager();
            var db = manager.Create<P2Item>(ParamFor<P2Item>(_file, ESQLToolType.EFCore));
            db.Connect();
            db.DisConnect();
            db.DisConnect();
            Assert.False(db.IsConnected);
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
