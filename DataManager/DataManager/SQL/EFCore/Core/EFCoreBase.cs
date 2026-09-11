using DataManager.SQL.Definition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DataManager.SQL.EFCore
{
    public abstract class EFCoreBase<T> : DbContext, ISQL<T>, ISplitTableModel where T : class
    {
        // 本库全链路使用 DateTime.Now/Today（Local），Npgsql 6+ 默认拒绝写入 Local 时间；
        // 此处在库层面保证兼容行为，不再依赖宿主（DataManagerModule/测试）是否设过开关。
        // 注意：开关必须在 Npgsql 首次使用前设置，静态构造满足该时序。
        static EFCoreBase() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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
                        con = $"server={SQLParam.ServerIP};port={SQLParam.Port};user={SQLParam.UserName};password={SQLParam.Password};database={SQLParam.DBName}";
                        optionsBuilder.UseMySQL(con);
                        break;

                    case ESQLType.PostgreSQL:
                        con = $"server={SQLParam.ServerIP} ;port= {SQLParam.Port} ;username= {SQLParam.UserName};password={SQLParam.Password};database={SQLParam.DBName}";
                        optionsBuilder.UseNpgsql(con);
                        break;

                    case ESQLType.SQLite:
                        // SQLite 无需账号端口；直通串优先（SQLParam 契约），否则走文件路径（目录预建，文件懒建）。
                        if (!string.IsNullOrWhiteSpace(SQLParam.ConnectionString))
                        {
                            optionsBuilder.UseSqlite(SQLParam.ConnectionString);
                            break;
                        }
                        var file = SQLParam.EffectiveFilePath;
                        var dir = Path.GetDirectoryName(Path.GetFullPath(file));
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                        optionsBuilder.UseSqlite($"Data Source={file}");
                        break;

                    case ESQLType.SQL_Server:
                        var server = string.IsNullOrWhiteSpace(SQLParam.Port)
                            ? SQLParam.ServerIP
                            : $"{SQLParam.ServerIP},{SQLParam.Port}";
                        con = $"Server={server};Database={SQLParam.DBName};User Id={SQLParam.UserName};Password={SQLParam.Password};TrustServerCertificate={(SQLParam.TrustServerCertificate ? "True" : "False")}";
                        optionsBuilder.UseSqlServer(con);
                        break;

                    default:
                        throw new NotSupportedException($"不支持的数据库类型: {SQLParam.DBType}");
                }
            }
            base.OnConfiguring(optionsBuilder);
        }

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

        protected EFCoreBase()
        {
        }

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

        private EFCoreBase<T> RouteFor(T data)
        {
            if (!IsSplitTableEntity())
                return this;
            return ForkForTable(GetSplitTableName(EFCoreSplitNaming.GetSplitFieldValue(data)));
        }

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

        private static void DisposeScoped(List<EFCoreBase<T>> scoped, EFCoreBase<T> self)
        {
            foreach (var s in scoped)
            {
                if (!ReferenceEquals(s, self))
                    s.Dispose();
            }
        }

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

        public virtual async Task AddAsync(T data)
        {
            if (!IsSplitTableEntity())
            {
                await Set<T>().AddAsync(data);
                await SaveChangesOptimizedAsync();
                return;
            }
            using var target = RouteFor(data);
            target.EnsureTableExists();
            await target.Set<T>().AddAsync(data);
            await target.SaveChangesOptimizedAsync();
        }

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

        public virtual async Task AddRangeAsync(IEnumerable<T> datas)
        {
            if (!IsSplitTableEntity())
            {
                await Set<T>().AddRangeAsync(datas);
                await SaveChangesOptimizedAsync();
                return;
            }
            foreach (var group in datas.GroupBy(d => GetSplitTableName(EFCoreSplitNaming.GetSplitFieldValue(d))))
            {
                using var target = ForkForTable(group.Key);
                target.EnsureTableExists();
                await target.Set<T>().AddRangeAsync(group);
                await target.SaveChangesOptimizedAsync();
            }
        }

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

        public virtual async Task DeleteAsync(Expression<Func<T, bool>> predicate)
        {
            var scoped = ScopedForQuery(predicate);
            try
            {
                foreach (var s in scoped)
                {
                    var entities = await s.Set<T>().Where(predicate).ToListAsync();
                    if (entities.Count == 0) continue;
                    s.Set<T>().RemoveRange(entities);
                    await s.SaveChangesAsync();
                }
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

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

        public virtual async Task DeleteAllAsync()
        {
            var scoped = ScopedForQuery();
            try
            {
                foreach (var s in scoped)
                    await s.Set<T>().ExecuteDeleteAsync();
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        public virtual IEnumerable<T> Query()
        {
            var scoped = ScopedForQuery();
            try
            {
                if (scoped.Count == 1)
                    return scoped[0].Set<T>().AsNoTracking().ToList();
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

        public virtual async Task<IEnumerable<T>> QueryAsync()
        {
            var scoped = ScopedForQuery();
            try
            {
                if (scoped.Count == 1)
                    return await scoped[0].Set<T>().AsNoTracking().ToListAsync();
                var all = new List<T>();
                foreach (var s in scoped)
                    all.AddRange(await s.Set<T>().AsNoTracking().ToListAsync());
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
                if (scoped.Count == 1)
                    return scoped[0].Set<T>().Where(predicate).AsNoTracking().ToList();
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
        public virtual async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate)
        {
            var scoped = ScopedForQuery(predicate);
            try
            {
                if (scoped.Count == 1)
                    return await scoped[0].Set<T>().Where(predicate).AsNoTracking().ToListAsync();
                var all = new List<T>();
                foreach (var s in scoped)
                    all.AddRange(await s.Set<T>().Where(predicate).AsNoTracking().ToListAsync());
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

        public virtual async Task UpdateAsync(T data)
        {
            if (!IsSplitTableEntity())
            {
                UpdateTrackedOrAttach(this, data);
                await SaveChangesOptimizedAsync();
                return;
            }
            using var target = RouteFor(data);
            target.Set<T>().Update(data);
            await target.SaveChangesOptimizedAsync();
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

        public virtual async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (IsSplitTableEntity())
                throw new NotSupportedException("分表实体暂不支持事务（fork 独立连接）。");
            if (_inTransaction)
                throw new InvalidOperationException("嵌套事务不受支持。");
            _inTransaction = true;
            try
            {
                using var tx = await Database.BeginTransactionAsync();
                try
                {
                    await action();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _inTransaction = false;
            }
        }

        private bool _inTransaction;

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

        public virtual async Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata)
        {
            var scoped = ScopedForQuery(predicate);
            try
            {
                foreach (var s in scoped)
                {
                    var entities = await s.Set<T>().Where(predicate).ToListAsync();
                    if (entities.Count == 0) continue;
                    foreach (var entity in entities)
                        CopyUpdatableValues(entity, updatedata);
                    await s.SaveChangesAsync();
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

        public async Task SaveChangesOptimizedAsync()
        {
            try
            {
                ChangeTracker.AutoDetectChangesEnabled = false;
                await base.SaveChangesAsync();
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
