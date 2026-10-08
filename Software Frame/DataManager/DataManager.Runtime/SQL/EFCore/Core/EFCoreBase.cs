// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：EFCoreBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DataManager.Runtime.SQL.EFCore
{
    /// <summary>
    /// EFCoreBase 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// 继承或实现的契约：DbContext、ISQL&lt;T&gt;、ISplitTableModel、ISplitTableNameProvider。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class EFCoreBase<T> : DbContext, ISQL<T>, ISplitTableModel, ISplitTableNameProvider where T : class
    {
        // 本库全链路使用 DateTime.Now/Today（Local），Npgsql 6+ 默认拒绝写入 Local 时间；
        // 此处在库层面保证兼容行为，不再依赖宿主（DataManagerModule/测试）是否设过开关。
        // 注意：开关必须在 Npgsql 首次使用前设置，静态构造满足该时序。
        static EFCoreBase() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnConfiguring。
        /// 正文中的关键协作调用包括 ReplaceService、IsNullOrWhiteSpace、UseMySQL、CommandTimeout、UseNpgsql；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 分表实例靠“表名覆盖”区分模型，必须先替换缓存键工厂再配 provider
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, SplitModelCacheKeyFactory>();
            if (!optionsBuilder.IsConfigured && SQLParam != null)
            {
                string con;

                switch (SQLParam.DBType)
                {
                    case ESQLType.MySQL:
                        // 直通串优先（SQLParam 全局契约：非空时覆盖分字段，同 SqlDialectHelper/MySqlDialect）。
                        // P9-4：统一命令超时映射到 provider 选项（0 → null → 保持 provider 默认，不改连接串）。
                        if (!string.IsNullOrWhiteSpace(SQLParam.ConnectionString))
                        {
                            optionsBuilder.UseMySQL(SQLParam.ConnectionString, o => o.CommandTimeout(SQLParam.CommandTimeoutOrNull));
                            break;
                        }
                        con = $"server={SQLParam.ServerIP};port={SQLParam.Port};user={SQLParam.UserName};password={SQLParam.Password};database={SQLParam.DBName}";
                        optionsBuilder.UseMySQL(con, o => o.CommandTimeout(SQLParam.CommandTimeoutOrNull));
                        break;

                    case ESQLType.PostgreSQL:
                        // 直通串优先（SQLParam 全局契约：非空时覆盖分字段，同 SqlDialectHelper/PostgresDialect）。
                        if (!string.IsNullOrWhiteSpace(SQLParam.ConnectionString))
                        {
                            optionsBuilder.UseNpgsql(SQLParam.ConnectionString, o => o.CommandTimeout(SQLParam.CommandTimeoutOrNull));
                            break;
                        }
                        con = $"server={SQLParam.ServerIP} ;port= {SQLParam.Port} ;username= {SQLParam.UserName};password={SQLParam.Password};database={SQLParam.DBName}";
                        optionsBuilder.UseNpgsql(con, o => o.CommandTimeout(SQLParam.CommandTimeoutOrNull));
                        break;

                    case ESQLType.SQLite:
                        // SQLite 无需账号端口；直通串优先（SQLParam 契约），否则走文件路径（目录预建，文件懒建）。
                        if (!string.IsNullOrWhiteSpace(SQLParam.ConnectionString))
                        {
                            optionsBuilder.UseSqlite(SQLParam.ConnectionString, o => o.CommandTimeout(SQLParam.CommandTimeoutOrNull));
                            break;
                        }
                        var file = SQLParam.EffectiveFilePath;
                        var dir = Path.GetDirectoryName(Path.GetFullPath(file));
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                        optionsBuilder.UseSqlite($"Data Source={file}", o => o.CommandTimeout(SQLParam.CommandTimeoutOrNull));
                        break;

                    case ESQLType.SQL_Server:
                        // 直通串优先（SQLParam 全局契约：非空时覆盖分字段，同 SqlDialectHelper/SqlServerDialect）。
                        if (!string.IsNullOrWhiteSpace(SQLParam.ConnectionString))
                        {
                            optionsBuilder.UseSqlServer(SQLParam.ConnectionString, o => o.CommandTimeout(SQLParam.CommandTimeoutOrNull));
                            break;
                        }
                        var server = string.IsNullOrWhiteSpace(SQLParam.Port)
                            ? SQLParam.ServerIP
                            : $"{SQLParam.ServerIP},{SQLParam.Port}";
                        con = $"Server={server};Database={SQLParam.DBName};User Id={SQLParam.UserName};Password={SQLParam.Password};TrustServerCertificate={(SQLParam.TrustServerCertificate ? "True" : "False")}";
                        optionsBuilder.UseSqlServer(con, o => o.CommandTimeout(SQLParam.CommandTimeoutOrNull));
                        break;

                    default:
                        throw new NotSupportedException($"不支持的数据库类型: {SQLParam.DBType}");
                }
            }
            base.OnConfiguring(optionsBuilder);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelCreating。
        /// 正文中的关键协作调用包括 ToTable、Entity、GetBaseTableName、OnModelCreating；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 未覆盖时显式映射“[Table] 或类名”，与此前 EF 默认行为一致（不做 PG 小写化）
            modelBuilder.Entity<T>().ToTable(SplitTableName ?? EFCoreSplitNaming.GetBaseTableName(typeof(T)));
            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// 数据库参数（构造期确定；P4-1 起只读，外部不再可替换）
        /// </summary>
        public virtual SQLParam SQLParam { get; protected set; } = new();

        /// <summary>
        /// 名称
        /// </summary>
        public virtual string Name { get; set; } = string.Empty;

        // P1-3：IsConnected 改为纯状态位，不再在 setter 中隐式 Connect/DisConnect（副作用难追踪）。
        // P4-1：setter 收为 private，仅 Connect/DisConnect 内部改写。
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 初始化 EFCoreBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        protected EFCoreBase()
        {
        }

        /// <summary>
        /// 初始化 EFCoreBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        protected EFCoreBase(SQLParam param)
        {
            SQLParam = param;
            Name = param.Name;
        }

        /// <summary>
        /// 分表模式下本实例命中的物理表名；非分表实体始终为 null。
        /// 由 <see cref="ForkForTable"/> 产生分表作用域实例时设置。
        /// </summary>
        public string? SplitTableName { get; protected set; }

        string? ISplitTableModel.SplitTableName => SplitTableName;
        ESQLType ISplitTableModel.SplitDbType => SQLParam?.DBType ?? default;

        /// <summary>
        /// 是否为分表实体（共享 DbSplitTableAttribute 或 Dapper 旧特性均可）。
        /// </summary>
        public bool IsSplitTableEntity() => EFCoreSplitNaming.IsSplitTableEntity(typeof(T));

        /// <summary>
        /// 按日期算分表名（公开以便测试与诊断；路由逻辑与 Dapper 一致）。
        /// </summary>
        public string GetSplitTableName(DateTime date) => EFCoreSplitNaming.GetSplitTableName(typeof(T), date);

        /// <summary>
        /// 产生“绑定到指定分表”的同构实例（SQLParam/Name 透传）。用完即 Dispose。
        /// </summary>
        public abstract EFCoreBase<T> ForkForTable(string tableName);

        /// <summary>
        /// 列出当前库中该实体的所有分表（需真实连接；无连接时抛）。
        /// </summary>
        public List<string> GetSplitTables()
        {
            // 注意：EF 物理表名保留大小写（不做 PG 小写化，与 Dapper 约定不同），LIKE 必须用原大小写；
            // PG 的 LIKE 大小写敏感，转小写会导致分表查不到（冒烟测试实证）。故此处不用方言的 Dapper 口径。
            var baseTable = EFCoreSplitNaming.GetBaseTableName(typeof(T));
            var sql = SQLParam.DBType switch
            {
                ESQLType.PostgreSQL => $"SELECT table_name FROM information_schema.tables WHERE table_name LIKE '{baseTable}_%'",
                ESQLType.MySQL => $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{SQLParam.DBName}' AND table_name LIKE '{baseTable}_%'",
                ESQLType.SQLite => $"SELECT name FROM sqlite_master WHERE type='table' AND name LIKE '{baseTable}_%'",
                ESQLType.SQL_Server => $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG = '{SQLParam.DBName}' AND TABLE_NAME LIKE '{baseTable}_%'",
                _ => throw new NotSupportedException($"不支持的数据库类型: {SQLParam.DBType}")
            };
            return Database.SqlQueryRaw<string>(sql).ToList();
        }

        /// <summary>
        /// 发起连接操作；对应入口为 Connect。
        /// 正文中的关键协作调用包括 EnsureCreated、IsSplitTableEntity、ForkForTable、GetSplitTableName、EnsureTableExists；具体条件和先后次序见方法体。
        /// </summary>
        public virtual void Connect()
        {
            // 注意：EnsureCreated 在库已存在时直接返回，不会补建缺失的表
            // （真库集成测试实证），故其后必须再做 EnsureTableExists。
            Database.EnsureCreated();
            if (IsSplitTableEntity())
            {
                using var current = ForkForTable(GetSplitTableName(DateTime.Now));
                current.EnsureTableExists();
            }
            else
            {
                EnsureTableExists();
            }
            IsConnected = true;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DisConnect。
        /// </summary>
        public virtual void DisConnect()
        {
            IsConnected = false;
        }

        /// <summary>
        /// 建本实例映射的表（如缺失）。模型恒单实体，直接调 EF 公开的 CreateTables()；
        /// 并发建表撞车（表已存在）视为成功。
        /// 背景：Database.EnsureCreated() 在“库存在且有表”时直接返回（见 EF 源码），
        /// 跨工具共库（Dapper 先建库）时 EF 的表永远建不出来，故必须补这一道。
        /// </summary>
        private void EnsureTableExists()
        {
            var entityType = Model.FindEntityType(typeof(T))
                ?? throw new InvalidOperationException($"实体 {typeof(T).Name} 未映射");
            var table = entityType.GetTableName()!;
            var schema = entityType.GetSchema()
                ?? (SQLParam.DBType switch
                {
                    ESQLType.MySQL => SQLParam.DBName,
                    ESQLType.SQLite => "main",
                    ESQLType.SQL_Server => "dbo",
                    _ => "public",
                });

            // 表/库名来自本库模型映射而非用户输入，无注入风险；用 ADO 直查以避开 EF1002
            Database.OpenConnection();
            try
            {
                using var cmd = Database.GetDbConnection().CreateCommand();
                if (SQLParam.DBType == ESQLType.SQLite)
                {
                    cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name = '" + table + "' LIMIT 1";
                }
                else if (SQLParam.DBType == ESQLType.SQL_Server)
                {
                    cmd.CommandText = "SELECT TOP 1 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '"
                        + schema + "' AND TABLE_NAME = '" + table + "'";
                }
                else
                {
                    cmd.CommandText = "SELECT 1 FROM information_schema.tables WHERE table_schema = '"
                        + schema + "' AND table_name = '" + table + "' LIMIT 1";
                }
                using var reader = cmd.ExecuteReader();
                if (reader.Read()) return;
            }
            finally
            {
                Database.CloseConnection();
            }

            try
            {
                this.GetService<IRelationalDatabaseCreator>().CreateTables();
            }
            catch (Exception ex) when (IsTableExistsRace(ex))
            {
                // 并发建表撞车：表已被他处建好，视为成功
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsTableExistsRace。
        /// 正文中的关键协作调用包括 Contains；具体条件和先后次序见方法体。
        /// </summary>
        private bool IsTableExistsRace(Exception ex)
        {
            return SQLParam.DBType switch
            {
                // PG duplicate_table
                ESQLType.PostgreSQL => ex is Npgsql.PostgresException pg && pg.SqlState == "42P07",
                // MySQL ER_TABLE_EXISTS_ERROR
                ESQLType.MySQL => ex is MySqlConnector.MySqlException my && my.Number == 1050,
                // SQLite table already exists
                ESQLType.SQLite => ex is Microsoft.Data.Sqlite.SqliteException lite
                    && lite.SqliteErrorCode == 1
                    && lite.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase),
                // SQL Server There is already an object named ...
                ESQLType.SQL_Server => ex is Microsoft.Data.SqlClient.SqlException ms && ms.Number is 2714 or 2715,
                _ => false,
            };
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RouteFor。
        /// 正文中的关键协作调用包括 IsSplitTableEntity、ForkForTable、GetSplitTableName、GetSplitFieldValue；具体条件和先后次序见方法体。
        /// </summary>
        private EFCoreBase<T> RouteFor(T data)
        {
            if (!IsSplitTableEntity())
                return this;
            return ForkForTable(GetSplitTableName(EFCoreSplitNaming.GetSplitFieldValue(data)));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ScopedForQuery。
        /// 正文中的关键协作调用包括 IsSplitTableEntity、GetSplitTables、Add、ForkForTable；具体条件和先后次序见方法体。
        /// </summary>
        private List<EFCoreBase<T>> ScopedForQuery(Expression<Func<T, bool>>? predicate = null)
        {
            if (!IsSplitTableEntity())
                return new List<EFCoreBase<T>> { this };
            var tables = predicate == null ? GetSplitTables() : GetSplitTables(predicate);
            var scoped = new List<EFCoreBase<T>>(tables.Count);
            foreach (var table in tables)
                scoped.Add(ForkForTable(table));
            return scoped;
        }

        /// <summary>
        /// P8：谓词定表（EF 口径：表名保留大小写，精确求交）。无可用约束则全表。
        /// </summary>
        private List<string> GetSplitTables(Expression<Func<T, bool>> predicate)
        {
            var splitProp = SplitDatePruner.FindSplitFieldProperty(typeof(T));
            if (splitProp == null)
                return GetSplitTables();
            var suffixes = SplitDatePruner.CandidateSuffixes(
                predicate, splitProp, DbSplitPeriod.PeriodOf(typeof(T)));
            if (suffixes == null)
                return GetSplitTables();
            var baseline = EFCoreSplitNaming.GetBaseTableName(typeof(T));
            var candidates = suffixes.Select(s => $"{baseline}_{s}").ToHashSet(StringComparer.Ordinal);
            return GetSplitTables().Where(t => candidates.Contains(t)).ToList();
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 DisposeScoped。
        /// 正文中的关键协作调用包括 ReferenceEquals、Dispose；具体条件和先后次序见方法体。
        /// </summary>
        private static void DisposeScoped(List<EFCoreBase<T>> scoped, EFCoreBase<T> self)
        {
            foreach (var s in scoped)
            {
                if (!ReferenceEquals(s, self))
                    s.Dispose();
            }
        }

        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// 正文中的关键协作调用包括 IsSplitTableEntity、Add、Set、SaveChangesOptimized、RouteFor；具体条件和先后次序见方法体。
        /// </summary>
        public virtual void Add(T data)
        {
            if (!IsSplitTableEntity())
            {
                Set<T>().Add(data);
                SaveChangesOptimized();
                return;
            }
            using var target = RouteFor(data);
            target.EnsureTableExists();
            target.Set<T>().Add(data);
            target.SaveChangesOptimized();
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task AddAsync(T data) => AddAsync(data, CancellationToken.None);

        /// <remarks>P9-3：Token 透传到 EF 原生可取消 API（AddAsync/SaveChangesAsync）；取消向上抛 OCE。</remarks>
        public virtual async Task AddAsync(T data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSplitTableEntity())
            {
                await Set<T>().AddAsync(data, cancellationToken);
                await SaveChangesOptimizedAsync(cancellationToken);
                return;
            }
            using var target = RouteFor(data);
            target.EnsureTableExists();
            await target.Set<T>().AddAsync(data, cancellationToken);
            await target.SaveChangesOptimizedAsync(cancellationToken);
        }

        /// <summary>
        /// 执行添加操作；对应入口为 AddRange。
        /// 正文中的关键协作调用包括 IsSplitTableEntity、AddRange、Set、SaveChangesOptimized、GroupBy；具体条件和先后次序见方法体。
        /// </summary>
        public virtual void AddRange(IEnumerable<T> datas)
        {
            if (!IsSplitTableEntity())
            {
                Set<T>().AddRange(datas);
                SaveChangesOptimized();
                return;
            }
            foreach (var group in datas.GroupBy(d => GetSplitTableName(EFCoreSplitNaming.GetSplitFieldValue(d))))
            {
                using var target = ForkForTable(group.Key);
                target.EnsureTableExists();
                target.Set<T>().AddRange(group);
                target.SaveChangesOptimized();
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task AddRangeAsync(IEnumerable<T> datas) => AddRangeAsync(datas, CancellationToken.None);

        /// <remarks>P9-3：Token 透传到 EF 原生可取消 API；分表按日期分组时逐组检查取消。</remarks>
        public virtual async Task AddRangeAsync(IEnumerable<T> datas, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSplitTableEntity())
            {
                await Set<T>().AddRangeAsync(datas, cancellationToken);
                await SaveChangesOptimizedAsync(cancellationToken);
                return;
            }
            foreach (var group in datas.GroupBy(d => GetSplitTableName(EFCoreSplitNaming.GetSplitFieldValue(d))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var target = ForkForTable(group.Key);
                target.EnsureTableExists();
                await target.Set<T>().AddRangeAsync(group, cancellationToken);
                await target.SaveChangesOptimizedAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 执行删除操作；对应入口为 Delete。
        /// 正文中的关键协作调用包括 ScopedForQuery、ToList、Where、Set、RemoveRange；具体条件和先后次序见方法体。
        /// </summary>
        public virtual void Delete(Expression<Func<T, bool>> predicate)
        {
            var scoped = ScopedForQuery(predicate);
            try
            {
                foreach (var s in scoped)
                {
                    var entities = s.Set<T>().Where(predicate).ToList();
                    if (entities.Count == 0) continue;
                    s.Set<T>().RemoveRange(entities);
                    s.SaveChanges();
                }
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task DeleteAsync(Expression<Func<T, bool>> predicate) => DeleteAsync(predicate, CancellationToken.None);

        /// <remarks>P9-3：Token 透传到 ToListAsync/SaveChangesAsync（EF 原生可取消）。</remarks>
        public virtual async Task DeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scoped = ScopedForQuery(predicate);
            try
            {
                foreach (var s in scoped)
                {
                    var entities = await s.Set<T>().Where(predicate).ToListAsync(cancellationToken);
                    if (entities.Count == 0) continue;
                    s.Set<T>().RemoveRange(entities);
                    await s.SaveChangesAsync(cancellationToken);
                }
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <summary>
        /// 执行删除操作；对应入口为 DeleteAll。
        /// 正文中的关键协作调用包括 ScopedForQuery、ExecuteDelete、Set、DisposeScoped；具体条件和先后次序见方法体。
        /// </summary>
        public virtual void DeleteAll()
        {
            var scoped = ScopedForQuery();
            try
            {
                foreach (var s in scoped)
                    s.Set<T>().ExecuteDelete();
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task DeleteAllAsync() => DeleteAllAsync(CancellationToken.None);

        /// <remarks>P9-3：Token 透传到 ExecuteDeleteAsync（EF 原生可取消）；分表逐表检查取消。</remarks>
        public virtual async Task DeleteAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scoped = ScopedForQuery();
            try
            {
                foreach (var s in scoped)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await s.Set<T>().ExecuteDeleteAsync(cancellationToken);
                }
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// 正文中的关键协作调用包括 ScopedForQuery、ToList、AsNoTracking、Set、AddRange；具体条件和先后次序见方法体。
        /// </summary>
        public virtual IEnumerable<T> Query()
        {
            var scoped = ScopedForQuery();
            try
            {
                var all = new List<T>();
                foreach (var s in scoped)
                    all.AddRange(s.Set<T>().AsNoTracking().ToList());
                return all;
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task<IEnumerable<T>> QueryAsync() => QueryAsync(CancellationToken.None);

        /// <remarks>P9-3：Token 透传到 ToListAsync（EF 原生可取消）；分表逐表检查取消。</remarks>
        public virtual async Task<IEnumerable<T>> QueryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scoped = ScopedForQuery();
            try
            {
                var all = new List<T>();
                foreach (var s in scoped)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    all.AddRange(await s.Set<T>().AsNoTracking().ToListAsync(cancellationToken));
                }
                return all;
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <remarks>
        /// P6-1：分表亦逐表服务端过滤（EF 原生翻译表达式，无需内存过滤；与 Dapper 手写路径不同）。
        /// </remarks>
        public virtual IEnumerable<T> Query(Expression<Func<T, bool>> predicate)
        {
            var scoped = ScopedForQuery(predicate);
            try
            {
                var all = new List<T>();
                foreach (var s in scoped)
                    all.AddRange(s.Set<T>().Where(predicate).AsNoTracking().ToList());
                return all;
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <remarks>P6-1：分表亦逐表服务端过滤（见同步重载说明）。</remarks>
        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate) =>
            QueryAsync(predicate, CancellationToken.None);

        /// <remarks>P6-1：分表亦逐表服务端过滤（见同步重载说明）。P9-3：Token 透传到 ToListAsync。</remarks>
        public virtual async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scoped = ScopedForQuery(predicate);
            try
            {
                var all = new List<T>();
                foreach (var s in scoped)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    all.AddRange(await s.Set<T>().Where(predicate).AsNoTracking().ToListAsync(cancellationToken));
                }
                return all;
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <summary>
        /// P9-3：单实体按主键更新（EF 原生语义：整行标记修改，null 会覆盖；分表按实体日期路由）。
        /// 已跟踪同键实例时直接 SetValues，避免“已跟踪”冲突（本库 DbContext 长驻，Add 后 Update 同一行必踩）。
        /// </summary>
        public virtual void Update(T data)
        {
            if (!IsSplitTableEntity())
            {
                UpdateTrackedOrAttach(this, data);
                SaveChangesOptimized();
                return;
            }
            using var target = RouteFor(data);
            target.Set<T>().Update(data);
            target.SaveChangesOptimized();
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task UpdateAsync(T data) => UpdateAsync(data, CancellationToken.None);

        /// <remarks>P9-3：Token 透传到 SaveChangesAsync（EF 原生可取消）。</remarks>
        public virtual async Task UpdateAsync(T data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSplitTableEntity())
            {
                UpdateTrackedOrAttach(this, data);
                await SaveChangesOptimizedAsync(cancellationToken);
                return;
            }
            using var target = RouteFor(data);
            target.Set<T>().Update(data);
            await target.SaveChangesOptimizedAsync(cancellationToken);
        }

        /// <summary>
        /// 同键已跟踪则整行设值，否则按新增跟踪附加（Update 语义）。
        /// 主键取 EF 模型元数据（支持复合主键）；无主键回退直接附加。
        /// </summary>
        private static void UpdateTrackedOrAttach(EFCoreBase<T> ctx, T data)
        {
            var keys = ctx.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties;
            if (keys == null || keys.Count == 0)
            {
                ctx.Set<T>().Update(data);
                return;
            }
            var tracked = ctx.Set<T>().Local.FirstOrDefault(e => keys.All(k =>
            {
                var a = k.PropertyInfo?.GetValue(e);
                var b = k.PropertyInfo?.GetValue(data);
                return Equals(a, b);
            }));
            if (tracked != null)
                ctx.Entry(tracked).CurrentValues.SetValues(data);
            else
                ctx.Set<T>().Update(data);
        }

        /// <summary>
        /// P10：单实例事务（显式连接事务；作用域内各操作 SaveChanges 共享同一事务）。
        /// 分表实体抛 NotSupportedException（fork 独立连接，无法加入本事务，静默非原子更危险）；
        /// 嵌套抛 InvalidOperationException。
        /// </summary>
        public virtual void ExecuteInTransaction(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (IsSplitTableEntity())
                throw new NotSupportedException("分表实体暂不支持事务（fork 独立连接）。");
            if (_inTransaction)
                throw new InvalidOperationException("嵌套事务不受支持。");
            _inTransaction = true;
            try
            {
                using var tx = Database.BeginTransaction();
                try
                {
                    action();
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            finally
            {
                _inTransaction = false;
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task ExecuteInTransactionAsync(Func<Task> action) =>
            ExecuteInTransactionAsync(action, CancellationToken.None);

        /// <remarks>
        /// P9-3：进入前与提交前各检查一次 Token；作用域内取消（OCE）与异常同样走回滚后向上抛。
        /// 回滚本身用 <see cref="CancellationToken.None"/>——回滚是取消的补救动作，绝不能被取消跳过。
        /// </remarks>
        public virtual async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSplitTableEntity())
                throw new NotSupportedException("分表实体暂不支持事务（fork 独立连接）。");
            if (_inTransaction)
                throw new InvalidOperationException("嵌套事务不受支持。");
            _inTransaction = true;
            try
            {
                using var tx = await Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await action();
                    cancellationToken.ThrowIfCancellationRequested();
                    await tx.CommitAsync(cancellationToken);
                }
                catch
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }
            finally
            {
                _inTransaction = false;
            }
        }

        private bool _inTransaction;

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 Update。
        /// 正文中的关键协作调用包括 ScopedForQuery、ToList、Where、Set、CopyUpdatableValues；具体条件和先后次序见方法体。
        /// </summary>
        public virtual void Update(Expression<Func<T, bool>> predicate, T updatedata)
        {
            var scoped = ScopedForQuery(predicate);
            try
            {
                foreach (var s in scoped)
                {
                    var entities = s.Set<T>().Where(predicate).ToList();
                    if (entities.Count == 0) continue;
                    foreach (var entity in entities)
                        CopyUpdatableValues(entity, updatedata);
                    s.SaveChanges();
                }
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public virtual Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata) =>
            UpdateAsync(predicate, updatedata, CancellationToken.None);

        /// <remarks>P9-3：Token 透传到 ToListAsync/SaveChangesAsync（EF 原生可取消）。</remarks>
        public virtual async Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scoped = ScopedForQuery(predicate);
            try
            {
                foreach (var s in scoped)
                {
                    var entities = await s.Set<T>().Where(predicate).ToListAsync(cancellationToken);
                    if (entities.Count == 0) continue;
                    foreach (var entity in entities)
                        CopyUpdatableValues(entity, updatedata);
                    await s.SaveChangesAsync(cancellationToken);
                }
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        /// <summary>
        /// 仅复制可更新值：跳过 [Key] 主键；引用/可空类型跳过 null；非空值类型始终复制。
        /// 与 Dapper/SqlSugar 的非空覆盖语义对齐，且允许 int=0 这类有效值更新。
        /// </summary>
        protected static void CopyUpdatableValues(T target, T source)
        {
            var properties = typeof(T).GetProperties().Where(p => p.CanRead && p.CanWrite);
            foreach (var property in properties)
            {
                if (Attribute.IsDefined(property, typeof(KeyAttribute)))
                    continue;
                var newValue = property.GetValue(source);
                if (newValue == null)
                    continue;
                property.SetValue(target, newValue);
            }
        }

        /// <summary>
        /// 高性能保存更改
        /// </summary>
        public void SaveChangesOptimized()
        {
            try
            {
                ChangeTracker.AutoDetectChangesEnabled = false;
                base.SaveChanges();
                ChangeTracker.AutoDetectChangesEnabled = true;
                if (ChangeTracker.Entries().Count() > 100)
                    DetachEntities();
            }
            catch (Exception ex)
            {
                ChangeTracker.AutoDetectChangesEnabled = true;
                throw new Exception($"保存数据时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task SaveChangesOptimizedAsync() => SaveChangesOptimizedAsync(CancellationToken.None);

        /// <remarks>
        /// P9-3：Token 透传到 base.SaveChangesAsync。
        /// 取消（<see cref="OperationCanceledException"/>）必须原样向上传播——若被下面那条
        /// 通用 catch 包装成普通 Exception，上层就无法把「取消」与「业务失败」区分开（P9 闸门第 3 条）。
        /// </remarks>
        public async Task SaveChangesOptimizedAsync(CancellationToken cancellationToken)
        {
            try
            {
                ChangeTracker.AutoDetectChangesEnabled = false;
                await base.SaveChangesAsync(cancellationToken);
                ChangeTracker.AutoDetectChangesEnabled = true;
                if (ChangeTracker.Entries().Count() > 100)
                    DetachEntities();
            }
            catch (OperationCanceledException)
            {
                ChangeTracker.AutoDetectChangesEnabled = true;
                throw;
            }
            catch (Exception ex)
            {
                ChangeTracker.AutoDetectChangesEnabled = true;
                throw new Exception($"保存数据时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 分离所有实体以释放内存
        /// </summary>
        private void DetachEntities()
        {
            var entries = ChangeTracker.Entries().ToList();
            foreach (var entry in entries)
            {
                if (entry.Entity != null)
                    entry.State = EntityState.Detached;
            }
        }
    }
}
