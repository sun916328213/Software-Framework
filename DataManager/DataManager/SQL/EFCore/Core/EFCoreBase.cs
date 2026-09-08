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
        /// 数据库参数
        /// </summary>
        public virtual SQLParam SQLParam { get; set; } = new();

        /// <summary>
        /// 名称
        /// </summary>
        public virtual string Name { get; set; } = string.Empty;

        // P1-3：IsConnected 改为纯状态位，不再在 setter 中隐式 Connect/DisConnect（副作用难追踪）。
        public bool IsConnected { get; set; }

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
            var baseTable = EFCoreSplitNaming.GetBaseTableName(typeof(T));
            var sql = SQLParam.DBType switch
            {
                ESQLType.PostgreSQL => $"SELECT table_name FROM information_schema.tables WHERE table_name LIKE '{baseTable}_%'",
                ESQLType.MySQL => $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{SQLParam.DBName}' AND table_name LIKE '{baseTable}_%'",
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
                ?? (SQLParam.DBType == ESQLType.MySQL ? SQLParam.DBName : "public");

            // 表/库名来自本库模型映射而非用户输入，无注入风险；用 ADO 直查以避开 EF1002
            Database.OpenConnection();
            try
            {
                using var cmd = Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT 1 FROM information_schema.tables WHERE table_schema = '"
                    + schema + "' AND table_name = '" + table + "' LIMIT 1";
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
                _ => false,
            };
        }

        private EFCoreBase<T> RouteFor(T data)
        {
            if (!IsSplitTableEntity())
                return this;
            return ForkForTable(GetSplitTableName(EFCoreSplitNaming.GetSplitFieldValue(data)));
        }

        private List<EFCoreBase<T>> ScopedForQuery()
        {
            if (!IsSplitTableEntity())
                return new List<EFCoreBase<T>> { this };
            var tables = GetSplitTables();
            var scoped = new List<EFCoreBase<T>>(tables.Count);
            foreach (var table in tables)
                scoped.Add(ForkForTable(table));
            return scoped;
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
            var scoped = ScopedForQuery();
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
            var scoped = ScopedForQuery();
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

        public virtual IEnumerable<T> Query(Expression<Func<T, bool>> predicate)
        {
            var scoped = ScopedForQuery();
            try
            {
                if (scoped.Count == 1)
                    return scoped[0].Set<T>().Where(predicate).AsNoTracking().ToList();
                // 分表：逐表查后内存过滤（与 Dapper 一致的性能取舍，大表慎用）
                var all = new List<T>();
                foreach (var s in scoped)
                    all.AddRange(s.Set<T>().AsNoTracking().ToList());
                return all.AsEnumerable().Where(predicate.Compile()).ToList();
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        public virtual async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate)
        {
            var scoped = ScopedForQuery();
            try
            {
                if (scoped.Count == 1)
                    return await scoped[0].Set<T>().Where(predicate).AsNoTracking().ToListAsync();
                var all = new List<T>();
                foreach (var s in scoped)
                    all.AddRange(await s.Set<T>().AsNoTracking().ToListAsync());
                return all.AsEnumerable().Where(predicate.Compile()).ToList();
            }
            finally
            {
                DisposeScoped(scoped, this);
            }
        }

        public virtual void Update(Expression<Func<T, bool>> predicate, T updatedata)
        {
            var scoped = ScopedForQuery();
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
            var scoped = ScopedForQuery();
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
