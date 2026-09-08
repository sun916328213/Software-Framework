using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

namespace DataManager.SQL.EFCore
{
    /// <summary>
    /// 分表模型标识：让模型缓存键工厂能读到“表名覆盖 + 数据库类型”。
    /// </summary>
    internal interface ISplitTableModel
    {
        string? SplitTableName { get; }
        ESQLType SplitDbType { get; }
    }

    /// <summary>
    /// P1+分表对齐：EF 模型默认按 DbContext 类型缓存，同一实体分多张表时必须把表名打进缓存键，
    /// 否则后创建的实例会复用首个实例的表映射。DbType 同理（同实体连 PG/MySQL 模型不同）。
    /// 警告：designTime 标记必须参与组键（默认工厂即如此）。若丢弃它，设计时模型请求会命中
    /// 运行时模型，导致建表/迁移管线报“The requested configuration is not stored in the
    /// read-optimized model”（真库集成测试实证）。
    /// </summary>
    internal sealed class SplitModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            if (context is ISplitTableModel m)
                return (context.GetType(), m.SplitTableName, m.SplitDbType, designTime);
            return (context.GetType(), (string?)null, default(ESQLType), designTime);
        }
    }

    /// <summary>
    /// EFCore 分表命名与路由（与 Dapper 语义对齐；同时识别共享特性与 Dapper 旧特性）。
    /// 物理表名规则：[Table] 特性名或类名 + "_" + 周期后缀（Day=yyyyMMdd/Month=yyyyMM/Year=yyyy）。
    /// 注意：沿用 EF 当前默认映射（不做 PG 小写化），故同一实体在 EF 与 Dapper 下的物理表名可能大小写不同，
    /// 各工具管理各自的表；行为（自动路由）三路一致。
    /// </summary>
    internal static class EFCoreSplitNaming
    {
        public static bool IsSplitTableEntity(Type entityType)
        {
            return entityType.GetCustomAttribute<DbSplitTableAttribute>() != null
                || entityType.GetCustomAttribute<DapperSplitTableAttribute>() != null;
        }

        public static string GetBaseTableName(Type entityType)
        {
            return entityType.GetCustomAttribute<TableAttribute>()?.Name ?? entityType.Name;
        }

        public static string GetSplitTableName(Type entityType, DateTime date)
        {
            var shared = entityType.GetCustomAttribute<DbSplitTableAttribute>();
            SplitPeriod period;
            if (shared != null)
            {
                period = shared.Period;
            }
            else
            {
                var legacy = entityType.GetCustomAttribute<DapperSplitTableAttribute>()
                    ?? throw new InvalidOperationException($"实体 {entityType.Name} 未标记分表特性");
                period = legacy.SplitType switch
                {
                    Dapper.SplitType.Day => SplitPeriod.Day,
                    Dapper.SplitType.Month => SplitPeriod.Month,
                    Dapper.SplitType.Year => SplitPeriod.Year,
                    _ => throw new NotSupportedException($"不支持的分表类型: {legacy.SplitType}")
                };
            }

            var suffix = period switch
            {
                SplitPeriod.Day => date.ToString("yyyyMMdd"),
                SplitPeriod.Month => date.ToString("yyyyMM"),
                SplitPeriod.Year => date.ToString("yyyy"),
                _ => throw new NotSupportedException($"不支持的分表类型: {period}")
            };
            return $"{GetBaseTableName(entityType)}_{suffix}";
        }

        public static DateTime GetSplitFieldValue<T>(T entity)
        {
            var properties = typeof(T).GetProperties();
            foreach (var prop in properties)
            {
                if (prop.GetCustomAttribute<DbSplitFieldAttribute>() != null
                    || prop.GetCustomAttribute<DapperSplitFieldAttribute>() != null)
                {
                    if (prop.GetValue(entity) is DateTime date)
                        return date;
                }
            }
            foreach (var prop in properties)
            {
                var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (propType == typeof(DateTime) && prop.CanRead && prop.CanWrite)
                {
                    if (prop.GetValue(entity) is DateTime date)
                        return date;
                }
            }
            throw new InvalidOperationException($"实体 {typeof(T).Name} 中未找到DateTime类型的字段");
        }
    }
}
