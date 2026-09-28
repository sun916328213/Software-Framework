using Dapper;
using Dommel;
using DataManager.SQL.Definition;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// Dapper + Dommel 数据库操作类（P4-2 瘦身后：仅做编排；
    /// SQL 构造归 DapperSqlBuilder，分表路由归 DapperSplitRouter，连接归 DapperConnectionFactory）。
    /// 支持分表功能（通过特性标记）。
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public class DapperBase<T> : ISQL<T> where T : class
    {
        // 同 EFCoreBase：库层面保证 Npgsql Local 时间兼容（静态构造时序早于首次连接）。
        static DapperBase()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            DapperDommelSetup.Ensure();
        }

        public SQLParam SQLParam { get; private set; } = new();

        public bool IsConnected { get; private set; }
        public virtual string Name { get; set; } = string.Empty;

        // P10：环境事务（单实例一次一事务；嵌套抛）。作用域内共享同一连接 + 显式 enlist。
        private IDbConnection? _txConn;
        private IDbTransaction? _tx;
        private IDbTransaction? Tx => _tx;

        private DapperSplitRouter<T> Split => new(SQLParam) { Tx = _tx };

        public DapperBase()
        {
        }

        public DapperBase(SQLParam sqlParam)
        {
            SQLParam = sqlParam;
            Name = sqlParam.Name;
        }

        /// <summary>
        /// 短连接：每次调用新建 + Open，调用方 using 释放（P0-4 线程安全改造）。
        /// P10：事务作用域内返回共享连接 + 空释放器（释放器 Dispose 才真正关闭）。
        /// </summary>
        private IDisposable OpenScoped(out IDbConnection conn)
        {
            if (_txConn != null)
            {
                conn = _txConn;
                return NullScope.Instance;
            }
            var fresh = new DapperConnectionFactory(SQLParam).CreateOpen();
            conn = fresh;
            return fresh;
        }

        private sealed class NullScope : IDisposable
        {
            internal static readonly NullScope Instance = new();
            public void Dispose() { }
        }

        /// <summary>
        /// P10：单实例事务。同连接原子提交，异常回滚；分表同连接原子；嵌套抛。
        /// </summary>
        public void ExecuteInTransaction(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (_tx != null)
                throw new InvalidOperationException("嵌套事务不受支持。");
            _txConn = new DapperConnectionFactory(SQLParam).CreateOpen();
            try
            {
                _tx = _txConn.BeginTransaction();
                try
                {
                    action();
                    _tx.Commit();
                }
                catch
                {
                    _tx.Rollback();
                    throw;
                }
            }
            finally
            {
                _tx?.Dispose();
                _tx = null;
                _txConn?.Dispose();
                _txConn = null;
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task ExecuteInTransactionAsync(Func<Task> action) =>
            ExecuteInTransactionAsync(action, CancellationToken.None);

        /// <remarks>
        /// P9-3：进入前与提交前各检查一次 Token；作用域内取消（OCE）与异常同样走回滚后向上抛。
        /// 回滚本身不受 Token 影响——回滚是取消的补救动作，绝不能被取消跳过。
        /// </remarks>
        public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();
            if (_tx != null)
                throw new InvalidOperationException("嵌套事务不受支持。");
            _txConn = new DapperConnectionFactory(SQLParam).CreateOpen();
            try
            {
                _tx = _txConn.BeginTransaction();
                try
                {
                    await action();
                    cancellationToken.ThrowIfCancellationRequested();
                    _tx.Commit();
                }
                catch
                {
                    _tx.Rollback();
                    throw;
                }
            }
            finally
            {
                _tx?.Dispose();
                _tx = null;
                _txConn?.Dispose();
                _txConn = null;
            }
        }

        public void Connect()
        {
            // 1. 检查数据库是否存在，不存在则创建
            new DapperConnectionFactory(SQLParam).EnsureDatabase();

            // 2. 短连接探活（不缓存连接）
            using var releaser = OpenScoped(out var conn);
            IsConnected = true;

            // 3. 创建表
            CreateTableIfNotExists();
        }

        /// <summary>
        /// 创建表（如果不存在）
        /// </summary>
        private void CreateTableIfNotExists()
        {
            try
            {
                using var releaser = OpenScoped(out var conn);
                var tableName = DapperSplitRouter<T>.IsSplitTableEntity()
                    ? Split.GetSplitTableName(DateTime.Now)
                    : (typeof(T).GetCustomAttribute<TableAttribute>()?.Name ?? typeof(T).Name);

                var actualTableName = SqlNamingHelper.ConvertTableName(tableName, SQLParam.DBType);

                // 检查表是否存在
                var checkSql = SqlDialectHelper.BuildCheckTableExistsSql(actualTableName, SQLParam.DBName, SQLParam.DBType);
                bool tableExists = conn.ExecuteScalar<int?>(checkSql, transaction: _tx) != null;

                // 表不存在则创建
                if (!tableExists)
                {
                    conn.Execute(DapperSqlBuilder<T>.GenerateCreateTableSql(SQLParam, actualTableName), transaction: _tx);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"警告: 创建表失败 - {ex.Message}");
            }
        }

        public void DisConnect()
        {
            // 短连接模式：无缓存连接可关，仅置状态位
            IsConnected = false;
        }

        public void Add(T data)
        {
            using var releaser = OpenScoped(out var conn);

            // 如果是分表实体，自动根据数据中的日期字段路由到对应分表
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var tableName = Split.GetSplitTableName(Split.GetSplitFieldValue(data));
                var actualTableName = SqlNamingHelper.ConvertTableName(tableName, SQLParam.DBType);
                Split.InsertToTable(conn, actualTableName, data);
            }
            else
            {
                conn.Insert(data, transaction: _tx);
            }
        }

        /// <summary>
        /// 仅复制可更新值：跳过 [Key] 主键（否则会把 Dommel/EF 查回的主键覆盖为 0，
        /// 导致 Update 的 WHERE 命中 0 行）；引用/可空类型跳过 null。
        /// </summary>
        private static void CopyUpdatableValues(T target, T source)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;
                if (prop.GetCustomAttributes(typeof(KeyAttribute), false).Any())
                    continue;
                var newValue = prop.GetValue(source);
                if (newValue == null)
                    continue;
                prop.SetValue(target, newValue);
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task AddAsync(T data) => AddAsync(data, CancellationToken.None);

        /// <remarks>
        /// P9-3：非分表走 Dommel 可取消重载；分表走 CommandDefinition（手写 SQL，原生可取消）。
        /// 边界：短连接 <c>Open()</c> 是第三方同步 API，无取消入口。
        /// </remarks>
        public async Task AddAsync(T data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var releaser = OpenScoped(out var conn);
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var tableName = Split.GetSplitTableName(Split.GetSplitFieldValue(data));
                await Split.InsertToTableAsync(conn, tableName, data, cancellationToken);
                return;
            }
            await conn.InsertAsync(data, transaction: _tx, cancellationToken: cancellationToken);
        }

        public void AddRange(IEnumerable<T> datas)
        {
            using var releaser = OpenScoped(out var conn);

            // 如果是分表实体，自动根据每条数据的日期路由
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                foreach (var data in datas)
                {
                    var tableName = Split.GetSplitTableName(Split.GetSplitFieldValue(data));
                    var actualTableName = SqlNamingHelper.ConvertTableName(tableName, SQLParam.DBType);
                    Split.InsertToTable(conn, actualTableName, data);
                }
            }
            else
            {
                foreach (var data in datas)
                {
                    conn.Insert(data, transaction: _tx);
                }
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task AddRangeAsync(IEnumerable<T> datas) => AddRangeAsync(datas, CancellationToken.None);

        /// <remarks>P9-3：逐条检查取消（分表按每条数据的日期路由）。</remarks>
        public async Task AddRangeAsync(IEnumerable<T> datas, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var releaser = OpenScoped(out var conn);

            // 如果是分表实体，自动根据每条数据的日期路由
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                foreach (var data in datas)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tableName = Split.GetSplitTableName(Split.GetSplitFieldValue(data));
                    await Split.InsertToTableAsync(conn, tableName, data, cancellationToken);
                }
            }
            else
            {
                foreach (var data in datas)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await conn.InsertAsync(data, transaction: _tx, cancellationToken: cancellationToken);
                }
            }
        }

        public IEnumerable<T> Query()
        {
            using var releaser = OpenScoped(out var conn);

            // 如果是分表实体，查询所有分表
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var allData = new List<T>();
                foreach (var table in Split.GetSplitTables())
                    allData.AddRange(Split.QueryTable(conn, table));
                return allData;
            }

            return conn.GetAll<T>(transaction: _tx);
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task<IEnumerable<T>> QueryAsync() => QueryAsync(CancellationToken.None);

        /// <remarks>P9-3：非分表走 Dommel 可取消重载；分表逐表检查取消并走 CommandDefinition。</remarks>
        public async Task<IEnumerable<T>> QueryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var releaser = OpenScoped(out var conn);

            // 如果是分表实体，查询所有分表
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var allData = new List<T>();
                foreach (var table in Split.GetSplitTables())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    allData.AddRange(await Split.QueryTableAsync(conn, table, cancellationToken));
                }
                return allData;
            }

            return await conn.GetAllAsync<T>(transaction: _tx, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 条件查询。
        /// </summary>
        /// <remarks>
        /// 分表实体：谓词可翻译则逐表服务端过滤（P6-2），否则回退全表查+内存过滤；
        /// 谓词含分表字段等值约束时仅查命中表（P7-1 裁剪）。大表慎用。
        /// </remarks>
        public IEnumerable<T> Query(Expression<Func<T, bool>> predicate)
        {
            using var releaser = OpenScoped(out var conn);

            // 分表实体：优先服务端过滤，不可翻译才内存过滤
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                if (DapperPredicateTranslator<T>.TryTranslate(predicate, SQLParam.DBType, out var where, out var pars))
                {
                    var pushed = new List<T>();
                    foreach (var table in Split.TablesFor(conn, predicate))
                        pushed.AddRange(Split.QueryWhere(conn, table, where, pars));
                    return pushed;
                }

                var allData = new List<T>();
                foreach (var table in Split.TablesFor(conn, predicate))
                    allData.AddRange(Split.QueryTable(conn, table));

                return allData.AsEnumerable().Where(predicate.Compile());
            }

            return conn.Select(predicate, transaction: _tx);
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate) =>
            QueryAsync(predicate, CancellationToken.None);

        /// <remarks>分表实体：可译下推，否则全表查+内存过滤，大表慎用。P9-3：Token 透传到各查询语句。</remarks>
        public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var releaser = OpenScoped(out var conn);

            // 分表实体：优先服务端过滤，不可翻译才内存过滤
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                if (DapperPredicateTranslator<T>.TryTranslate(predicate, SQLParam.DBType, out var where, out var pars))
                {
                    var pushed = new List<T>();
                    foreach (var table in Split.TablesFor(conn, predicate))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        pushed.AddRange(await Split.QueryWhereAsync(conn, table, where, pars, cancellationToken));
                    }
                    return pushed;
                }

                var allData = new List<T>();
                foreach (var table in Split.TablesFor(conn, predicate))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    allData.AddRange(await Split.QueryTableAsync(conn, table, cancellationToken));
                }

                return allData.AsEnumerable().Where(predicate.Compile());
            }

            return await conn.SelectAsync(predicate, transaction: _tx, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 更新实体
        /// </summary>
        public void Update(T data)
        {
            using var releaser = OpenScoped(out var conn);

            // 分表实体需要特殊处理
            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var tableName = Split.GetSplitTableName(Split.GetSplitFieldValue(data));
                Split.UpdateInSplitTable(conn, tableName, data);
            }
            else
            {
                conn.Update(data, transaction: _tx);
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task UpdateAsync(T data) => UpdateAsync(data, CancellationToken.None);

        /// <remarks>
        /// P9-3：非分表走 Dommel 可取消重载；分表走 CommandDefinition。
        /// 现状保持：非分表分支历史上未携带环境事务（与同步重载 <c>Update(data, transaction: _tx)</c> 不一致），
        /// P9 只加 Token、不改事务语义，该差异已登记待独立处理。
        /// </remarks>
        public async Task UpdateAsync(T data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var releaser = OpenScoped(out var conn);

            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var tableName = Split.GetSplitTableName(Split.GetSplitFieldValue(data));
                await Split.UpdateInSplitTableAsync(conn, tableName, data, cancellationToken);
                return;
            }
            await conn.UpdateAsync(data, cancellationToken: cancellationToken);
        }

        /// <remarks>
        /// 分表实体：定位行阶段可译下推 + 等值日期裁剪（取行后仍逐行更新），大表慎用。
        /// 约束：不要更新分表字段本身（行不迁移，裁剪查询可能 miss）。
        /// </remarks>
        public void Update(Expression<Func<T, bool>> predicate, T updatedata)
        {
            using var releaser = OpenScoped(out var conn);

            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var pushed = DapperPredicateTranslator<T>.TryTranslate(predicate, SQLParam.DBType, out var where, out var pars);
                foreach (var table in Split.TablesFor(conn, predicate))
                {
                    var entities = pushed
                        ? Split.QueryWhere(conn, table, where, pars)
                        : Split.QueryTable(conn, table).AsEnumerable().Where(predicate.Compile()).ToList();

                    foreach (var entity in entities)
                    {
                        CopyUpdatableValues(entity, updatedata);
                        Split.UpdateInSplitTable(conn, table, entity);
                    }
                }
            }
            else
            {
                var entities = conn.Select(predicate, transaction: _tx).ToList();
                foreach (var entity in entities)
                {
                    CopyUpdatableValues(entity, updatedata);
                    conn.Update(entity, transaction: _tx);
                }
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata) =>
            UpdateAsync(predicate, updatedata, CancellationToken.None);

        /// <remarks>分表实体：定位行阶段可译下推（取行后仍逐行更新），大表慎用。P9-3：逐表/逐行检查取消。</remarks>
        public async Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var releaser = OpenScoped(out var conn);

            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var pushed = DapperPredicateTranslator<T>.TryTranslate(predicate, SQLParam.DBType, out var where, out var pars);
                foreach (var table in Split.TablesFor(conn, predicate))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var allData = pushed
                        ? await Split.QueryWhereAsync(conn, table, where, pars, cancellationToken)
                        : await Split.QueryTableAsync(conn, table, cancellationToken);
                    var entities = pushed ? allData : allData.AsEnumerable().Where(predicate.Compile()).ToList();

                    foreach (var entity in entities)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        CopyUpdatableValues(entity, updatedata);
                        await Split.UpdateInSplitTableAsync(conn, table, entity, cancellationToken);
                    }
                }
            }
            else
            {
                var entities = (await conn.SelectAsync(predicate, transaction: _tx, cancellationToken: cancellationToken)).ToList();
                foreach (var entity in entities)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CopyUpdatableValues(entity, updatedata);
                    await conn.UpdateAsync(entity, transaction: _tx, cancellationToken: cancellationToken);
                }
            }
        }

        /// <remarks>分表实体：定位行阶段可译下推，大表慎用。</remarks>
        public void Delete(Expression<Func<T, bool>> predicate)
        {
            using var releaser = OpenScoped(out var conn);

            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var pushed = DapperPredicateTranslator<T>.TryTranslate(predicate, SQLParam.DBType, out var where, out var pars);
                foreach (var table in Split.TablesFor(conn, predicate))
                {
                    var entities = pushed
                        ? Split.QueryWhere(conn, table, where, pars)
                        : Split.QueryTable(conn, table).AsEnumerable().Where(predicate.Compile()).ToList();

                    foreach (var entity in entities)
                    {
                        Split.DeleteFromSplitTable(conn, table, entity);
                    }
                }
            }
            else
            {
                var entities = conn.Select(predicate, transaction: _tx).ToList();
                foreach (var entity in entities)
                {
                    conn.Delete(entity, transaction: _tx);
                }
            }
        }

        /// <summary>旧签名（无 Token）：等价于传 <see cref="CancellationToken.None"/>。</summary>
        public Task DeleteAsync(Expression<Func<T, bool>> predicate) =>
            DeleteAsync(predicate, CancellationToken.None);

        /// <remarks>
        /// P4-2 补齐：分表实体走逐表删除（此前异步版漏掉分表分支，静默只删默认表）。
        /// 定位行阶段可译下推（P6-2），大表慎用。P9-3：逐表/逐行检查取消。
        /// 现状保持：非分表分支的历史定位查询未携带环境事务（与同步重载不一致），P9 只加 Token。
        /// </remarks>
        public async Task DeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var releaser = OpenScoped(out var conn);

            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                var pushed = DapperPredicateTranslator<T>.TryTranslate(predicate, SQLParam.DBType, out var where, out var pars);
                foreach (var table in Split.TablesFor(conn, predicate))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var allData = pushed
                        ? await Split.QueryWhereAsync(conn, table, where, pars, cancellationToken)
                        : await Split.QueryTableAsync(conn, table, cancellationToken);
                    var entities = pushed ? allData : allData.AsEnumerable().Where(predicate.Compile()).ToList();

                    foreach (var entity in entities)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Split.DeleteFromSplitTable(conn, table, entity);
                    }
                }
                return;
            }

            var normals = (await conn.SelectAsync(predicate, cancellationToken: cancellationToken)).ToList();
            foreach (var entity in normals)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await conn.DeleteAsync(entity, transaction: _tx, cancellationToken: cancellationToken);
            }
        }

        public void DeleteAll()
        {
            using var releaser = OpenScoped(out var conn);

            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                foreach (var table in Split.GetSplitTables(conn))
                    conn.Execute($"DELETE FROM {SqlNamingHelper.FormatTableName(table, SQLParam.DBType)}", transaction: _tx);
                return;
            }

            var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>();
            var baseTable = tableAttr?.Name ?? typeof(T).Name;
            conn.Execute(DapperSqlBuilder<T>.BuildDeleteAllSql(SQLParam, baseTable), transaction: _tx);
        }

        /// <remarks>P4-2 补齐：分表逐表 DELETE（此前只删基表名，分表数据残留）。P9-3：逐表检查取消。</remarks>
        public Task DeleteAllAsync() => DeleteAllAsync(CancellationToken.None);

        /// <remarks>P9-3：手写 DELETE 走 CommandDefinition，原生携带 Token 与命令超时。</remarks>
        public async Task DeleteAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var releaser = OpenScoped(out var conn);

            if (DapperSplitRouter<T>.IsSplitTableEntity())
            {
                foreach (var table in Split.GetSplitTables(conn))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await conn.ExecuteAsync(DapperCommandFactory.Create(
                        $"DELETE FROM {SqlNamingHelper.FormatTableName(table, SQLParam.DBType)}",
                        null, _tx, SQLParam, cancellationToken));
                }
                return;
            }

            var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>();
            var baseTable = tableAttr?.Name ?? typeof(T).Name;
            await conn.ExecuteAsync(DapperCommandFactory.Create(
                DapperSqlBuilder<T>.BuildDeleteAllSql(SQLParam, baseTable), null, _tx, SQLParam, cancellationToken));
        }

        public void Dispose()
        {
            DisConnect();
        }

        /// <summary>
        /// 获取所有分表名（P3-3：LIKE 语句下沉到方言，支持四库）。
        /// </summary>
        public List<string> GetSplitTables()
        {
            using var releaser = OpenScoped(out var conn);
            return Split.GetSplitTables(conn);
        }
    }
}
