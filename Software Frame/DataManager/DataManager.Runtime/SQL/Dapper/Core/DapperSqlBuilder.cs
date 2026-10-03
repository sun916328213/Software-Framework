// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：DapperSqlBuilder。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace DataManager.Runtime.SQL.Dapper
{
    /// <summary>
    /// P4-2：Dapper 手写 SQL 构造器（从 DapperBase 拆出，原逻辑逐行搬迁，行为不变）。
    /// 实体反射加 ConcurrentDictionary 缓存，避免每次 CRUD 重复反射。
    /// </summary>
    internal static class DapperSqlBuilder<T> where T : class
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propsCache = new();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EntityProperties。
        /// 正文中的关键协作调用包括 GetOrAdd、ToArray、Where、GetProperties；具体条件和先后次序见方法体。
        /// </summary>
        internal static PropertyInfo[] EntityProperties() =>
            _propsCache.GetOrAdd(typeof(T), t => t.GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray());

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 FindKeyProperty。
        /// 正文中的关键协作调用包括 FirstOrDefault、EntityProperties、Any、GetCustomAttributes；具体条件和先后次序见方法体。
        /// </summary>
        internal static PropertyInfo? FindKeyProperty() =>
            EntityProperties().FirstOrDefault(p => p.GetCustomAttributes(typeof(KeyAttribute), false).Any());

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsNullableType。
        /// 正文中的关键协作调用包括 GetGenericTypeDefinition；具体条件和先后次序见方法体。
        /// </summary>
        internal static bool IsNullableType(Type type) =>
            !type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));

        /// <summary>
        /// 是否为“未赋值的 [Key] 主键”（与 Dommel 自增语义对齐：默认key不显式插入，由库自增）。
        /// </summary>
        internal static bool IsDefaultKeyValue(PropertyInfo prop, T data)
        {
            if (!prop.GetCustomAttributes(typeof(KeyAttribute), false).Any())
                return false;
            var value = prop.GetValue(data);
            if (value == null)
                return true;
            var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (!type.IsValueType)
                return false;
            return value.Equals(Activator.CreateInstance(type));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 GenerateCreateTableSql。
        /// 正文中的关键协作调用包括 EntityProperties、GetCustomAttribute、Add、BuildPrimaryKeyDefinition、FormatColumnName；具体条件和先后次序见方法体。
        /// </summary>
        internal static string GenerateCreateTableSql(SQLParam param, string tableName)
        {
            var columns = new List<string>();

            foreach (var prop in EntityProperties())
            {
                if (prop.GetCustomAttribute<KeyAttribute>() != null)
                {
                    columns.Add(SqlDialectHelper.BuildPrimaryKeyDefinition(prop.Name, prop.PropertyType, param.DBType));
                    continue;
                }

                var columnDef = SqlNamingHelper.FormatColumnName(prop.Name, param.DBType);
                columnDef += $" {SqlDialectHelper.GetDbType(prop.PropertyType, param.DBType)}";
                if (!IsNullableType(prop.PropertyType))
                    columnDef += " NOT NULL";
                columns.Add(columnDef);
            }

            var wrappedTableName = SqlDialectHelper.WrapTableName(tableName, param.DBType);
            var suffix = SqlDialectHelper.GetCreateTableSuffix(param.DBType);

            // SQL Server 不支持 IF NOT EXISTS：调用前已判存，这里直建，撞车由上层吞掉即可。
            var createKeyword = param.DBType == ESQLType.SQL_Server
                ? $"CREATE TABLE {wrappedTableName}"
                : $"CREATE TABLE IF NOT EXISTS {wrappedTableName}";
            return $"{createKeyword} ({string.Join(", ", columns)}){suffix}";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildSelectAllSql。
        /// 正文中的关键协作调用包括 FormatTableName；具体条件和先后次序见方法体。
        /// </summary>
        internal static string BuildSelectAllSql(SQLParam param, string tableName) =>
            $"SELECT * FROM {SqlNamingHelper.FormatTableName(tableName, param.DBType)}";

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildSelectWhereSql。
        /// 正文中的关键协作调用包括 BuildSelectAllSql；具体条件和先后次序见方法体。
        /// </summary>
        internal static string BuildSelectWhereSql(SQLParam param, string tableName, string whereClause) =>
            $"{BuildSelectAllSql(param, tableName)} WHERE {whereClause}";

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildDeleteAllSql。
        /// 正文中的关键协作调用包括 FormatTableName；具体条件和先后次序见方法体。
        /// </summary>
        internal static string BuildDeleteAllSql(SQLParam param, string baseTableName) =>
            $"DELETE FROM {SqlNamingHelper.FormatTableName(baseTableName, param.DBType)}";

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildInsertSql。
        /// 正文中的关键协作调用包括 FormatColumnList、ToArray、Select、Join、FormatTableName；具体条件和先后次序见方法体。
        /// </summary>
        internal static string BuildInsertSql(SQLParam param, string tableName, IReadOnlyList<PropertyInfo> props)
        {
            var columns = SqlNamingHelper.FormatColumnList(props.Select(p => p.Name).ToArray(), param.DBType);
            var parameters = string.Join(", ", props.Select(p => $"@{p.Name}"));
            return $"INSERT INTO {SqlNamingHelper.FormatTableName(tableName, param.DBType)} ({columns}) VALUES ({parameters})";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildUpdateByKeySql。
        /// 正文中的关键协作调用包括 Join、Select、ConvertColumnName、FormatTableName；具体条件和先后次序见方法体。
        /// </summary>
        internal static string BuildUpdateByKeySql(SQLParam param, string tableName, PropertyInfo key, IEnumerable<PropertyInfo> others)
        {
            var setClause = string.Join(", ", others
                .Select(p => $"{SqlNamingHelper.ConvertColumnName(p.Name, param.DBType)} = @{p.Name}"));
            var whereClause = $"{SqlNamingHelper.ConvertColumnName(key.Name, param.DBType)} = @{key.Name}";
            return $"UPDATE {SqlNamingHelper.FormatTableName(tableName, param.DBType)} SET {setClause} WHERE {whereClause}";
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildDeleteByKeySql。
        /// 正文中的关键协作调用包括 ConvertColumnName、FormatTableName；具体条件和先后次序见方法体。
        /// </summary>
        internal static string BuildDeleteByKeySql(SQLParam param, string tableName, PropertyInfo key)
        {
            var whereClause = $"{SqlNamingHelper.ConvertColumnName(key.Name, param.DBType)} = @{key.Name}";
            return $"DELETE FROM {SqlNamingHelper.FormatTableName(tableName, param.DBType)} WHERE {whereClause}";
        }
    }
}
