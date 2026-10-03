// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ISplitTableModel、SplitModelCacheKeyFactory、EFCoreSplitNaming。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Dapper、DataManager.Core.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Dapper;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

namespace DataManager.Runtime.SQL.EFCore
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
        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// 正文中的关键协作调用包括 GetType；具体条件和先后次序见方法体。
        /// </summary>
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
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsSplitTableEntity。
        /// 正文中的关键协作调用包括 GetCustomAttribute；具体条件和先后次序见方法体。
        /// </summary>
        public static bool IsSplitTableEntity(Type entityType)
        {
            return entityType.GetCustomAttribute<DbSplitTableAttribute>() != null
                || entityType.GetCustomAttribute<DapperSplitTableAttribute>() != null;
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetBaseTableName。
        /// 正文中的关键协作调用包括 GetCustomAttribute；具体条件和先后次序见方法体。
        /// </summary>
        public static string GetBaseTableName(Type entityType)
        {
            return entityType.GetCustomAttribute<TableAttribute>()?.Name ?? entityType.Name;
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSplitTableName。
        /// 正文中的关键协作调用包括 GetCustomAttribute、ToString、GetBaseTableName；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetSplitFieldValue。
        /// 正文中的关键协作调用包括 GetProperties、GetCustomAttribute、GetValue、GetUnderlyingType；具体条件和先后次序见方法体。
        /// </summary>
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
