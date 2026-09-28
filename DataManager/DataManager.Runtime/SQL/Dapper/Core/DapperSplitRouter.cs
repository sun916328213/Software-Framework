using Dapper;
using DataManager.SQL.Definition;
using System;
using System.Collections.Generic;
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
    /// P4-2：Dapper 分表路由器（从 DapperBase 拆出，原逻辑逐行搬迁，行为不变）。
    /// P10：持有环境事务（同连接命令必须显式 enlist，否则 ADO.NET 抛“命令缺少事务对象”）。
    /// 连接由调用方（DapperBase 作用域）打开传入；无参重载自开短连接（仅非事务路径）。
    /// </summary>
    internal sealed class DapperSplitRouter<T> where T : class
    {
        private readonly SQLParam _param;

        internal DapperSplitRouter(SQLParam param, IDbTransaction? tx = null)
        {
            _param = param;
            Tx = tx;
        }

        internal IDbTransaction? Tx { get; set; }

        /// <summary>检查是否为分表实体（Dapper 旧特性或共享 DbSplitTableAttribute 均可）。</summary>
        internal static bool IsSplitTableEntity()
        {
            return typeof(T).GetCustomAttributes(typeof(DapperSplitTableAttribute), false).Any()
                || typeof(T).GetCustomAttributes(typeof(DbSplitTableAttribute), false).Any();
        }

        internal DateTime GetSplitFieldValue(T entity) =>
            EFCore.EFCoreSplitNaming.GetSplitFieldValue(entity);

        internal string GetSplitTableName(DateTime date)
        {
            // 使用类名作为基础表名
            var baseTableName = typeof(T).Name;
            var sharedAttr = typeof(T).GetCustomAttribute<DbSplitTableAttribute>();
            if (sharedAttr != null)
            {
                var sharedSuffix = sharedAttr.Period switch
                {
                    SplitPeriod.Day => date.ToString("yyyyMMdd"),
                    SplitPeriod.Month => date.ToString("yyyyMM"),
                    SplitPeriod.Year => date.ToString("yyyy"),
                    _ => throw new NotSupportedException($"不支持的分表类型: {sharedAttr.Period}")
                };
                return $"{baseTableName}_{sharedSuffix}";
            }

            var splitAttr = typeof(T).GetCustomAttribute<DapperSplitTableAttribute>();
            if (splitAttr == null)
                return baseTableName;

            var suffix = splitAttr.SplitType switch
            {
                SplitType.Day => date.ToString("yyyyMMdd"),
                SplitType.Month => date.ToString("yyyyMM"),
                SplitType.Year => date.ToString("yyyy"),
                _ => throw new NotSupportedException($"不支持的分表类型: {splitAttr.SplitType}")
            };
            return $"{baseTableName}_{suffix}";
        }

        internal List<string> GetSplitTables(IDbConnection conn)
        {
            var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>();
            var baseTableName = tableAttr?.Name ?? typeof(T).Name;
            var sql = SqlDialectRegistry.Get(_param.DBType).BuildSplitTablesLikeSql(baseTableName, _param.DBName);
            return conn.Query<string>(sql, transaction: Tx).ToList();
        }

        internal List<string> GetSplitTables()
        {
            if (Tx != null)
                throw new InvalidOperationException("事务内请使用 GetSplitTables(conn)。");
            using var conn = new DapperConnectionFactory(_param).CreateOpen();
            return GetSplitTables(conn);
        }

        /// <summary>
        /// P7-1/P8：谓词定表。等值/有界范围约束只返回命中表（与现存表求交，不存在即空集）；
        /// 无可用约束、单边无界、超上限一律全表（正确优先）。
        /// 约束：分表字段值原地更新后行不迁移——不要更新分表字段本身，否则裁剪查询可能 miss 脏行。
        /// </summary>
        internal List<string> TablesFor(IDbConnection conn, Expression<Func<T, bool>> predicate)
        {
            var pruned = TryPrune(predicate);
            if (pruned == null)
                return GetSplitTables(conn);
            var existing = GetSplitTables(conn);
            return existing.Where(t => pruned.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        internal List<string> TablesFor(Expression<Func<T, bool>> predicate)
        {
            // 非事务路径自开短连接；事务内必须用 TablesFor(conn, predicate)（Tx 绑定特定连接）。
            if (Tx != null)
                throw new InvalidOperationException("事务内请使用 TablesFor(conn, predicate)。");
            using var conn = new DapperConnectionFactory(_param).CreateOpen();
            return TablesFor(conn, predicate);
        }

        /// <summary>候选表名（小写）；无约束返回 null。</summary>
        private HashSet<string>? TryPrune(Expression<Func<T, bool>> predicate)
        {
            var splitProp = SplitDatePruner.FindSplitFieldProperty(typeof(T));
            if (splitProp == null)
                return null;
            var period = DbSplitPeriod.PeriodOf(typeof(T));
            var suffixes = SplitDatePruner.CandidateSuffixes(predicate, splitProp, period);
            if (suffixes == null)
                return null;
            var baseTableName = typeof(T).Name;
            return suffixes.Select(s => $"{baseTableName}_{s}".ToLowerInvariant()).ToHashSet();
        }

        internal void EnsureTable(IDbConnection conn, string tableName)
        {
            var actualTableName = SqlNamingHelper.ConvertTableName(tableName, _param.DBType);
            var checkSql = SqlDialectHelper.BuildCheckTableExistsSql(actualTableName, _param.DBName, _param.DBType);
            bool tableExists = conn.ExecuteScalar<int?>(checkSql, transaction: Tx) != null;
            if (!tableExists)
                conn.Execute(DapperSqlBuilder<T>.GenerateCreateTableSql(_param, actualTableName), transaction: Tx);
        }

        private static System.Reflection.PropertyInfo RequireKey() =>
            DapperSqlBuilder<T>.FindKeyProperty()
                ?? throw new InvalidOperationException("实体必须包含主键字段");

        private static List<System.Reflection.PropertyInfo> InsertableProps(T data)
        {
            var props = DapperSqlBuilder<T>.EntityProperties()
                .Where(p => !DapperSqlBuilder<T>.IsDefaultKeyValue(p, data))
                .ToList();
            if (props.Count == 0)
                props = DapperSqlBuilder<T>.EntityProperties().ToList();
            return props;
        }

        internal void InsertToTable(IDbConnection conn, string tableName, T data)
        {
            EnsureTable(conn, tableName);
            var props = InsertableProps(data);
            conn.Execute(DapperSqlBuilder<T>.BuildInsertSql(_param, tableName, props), data, transaction: Tx);
        }

        internal async Task InsertToTableAsync(IDbConnection conn, string tableName, T data, CancellationToken cancellationToken = default)
        {
            // 建表判存走同步（DDL 无真异步 API，原地执行；DML 走真异步并携带 Token + 命令超时）。
            cancellationToken.ThrowIfCancellationRequested();
            EnsureTable(conn, tableName);
            var props = InsertableProps(data);
            await conn.ExecuteAsync(DapperCommandFactory.Create(
                DapperSqlBuilder<T>.BuildInsertSql(_param, tableName, props), data, Tx, _param, cancellationToken));
        }

        internal void UpdateInSplitTable(IDbConnection conn, string tableName, T data)
        {
            var key = RequireKey();
            var others = DapperSqlBuilder<T>.EntityProperties().Where(p => p.Name != key.Name);
            conn.Execute(DapperSqlBuilder<T>.BuildUpdateByKeySql(_param, tableName, key, others), data, transaction: Tx);
        }

        internal async Task UpdateInSplitTableAsync(IDbConnection conn, string tableName, T data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = RequireKey();
            var others = DapperSqlBuilder<T>.EntityProperties().Where(p => p.Name != key.Name);
            await conn.ExecuteAsync(DapperCommandFactory.Create(
                DapperSqlBuilder<T>.BuildUpdateByKeySql(_param, tableName, key, others), data, Tx, _param, cancellationToken));
        }

        internal void DeleteFromSplitTable(IDbConnection conn, string tableName, T data)
        {
            var key = RequireKey();
            conn.Execute(DapperSqlBuilder<T>.BuildDeleteByKeySql(_param, tableName, key), data, transaction: Tx);
        }

        internal List<T> QueryTable(IDbConnection conn, string tableName) =>
            conn.Query<T>(DapperSqlBuilder<T>.BuildSelectAllSql(_param, tableName), transaction: Tx).ToList();

        internal async Task<List<T>> QueryTableAsync(IDbConnection conn, string tableName, CancellationToken cancellationToken = default) =>
            (await conn.QueryAsync<T>(DapperCommandFactory.Create(
                DapperSqlBuilder<T>.BuildSelectAllSql(_param, tableName), null, Tx, _param, cancellationToken))).ToList();

        internal List<T> QueryWhere(IDbConnection conn, string tableName, string whereClause, DynamicParameters pars) =>
            conn.Query<T>(DapperSqlBuilder<T>.BuildSelectWhereSql(_param, tableName, whereClause), pars, transaction: Tx).ToList();

        internal async Task<List<T>> QueryWhereAsync(IDbConnection conn, string tableName, string whereClause, DynamicParameters pars, CancellationToken cancellationToken = default) =>
            (await conn.QueryAsync<T>(DapperCommandFactory.Create(
                DapperSqlBuilder<T>.BuildSelectWhereSql(_param, tableName, whereClause), pars, Tx, _param, cancellationToken))).ToList();
    }
}
