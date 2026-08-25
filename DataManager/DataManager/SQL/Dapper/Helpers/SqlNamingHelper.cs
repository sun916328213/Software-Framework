using DataManager.SQL.Definition;
using System;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// SQL命名转换助手
    /// 统一处理不同数据库的表名和字段名大小写问题
    /// </summary>
    public static class SqlNamingHelper
    {
        /// <summary>
        /// 转换表名
        /// </summary>
        public static string ConvertTableName(string tableName, ESQLType dbType)
        {
            if (string.IsNullOrEmpty(tableName))
                return tableName;

            return dbType switch
            {
                ESQLType.PostgreSQL => tableName.ToLower(),
                ESQLType.MySQL => tableName,
                _ => tableName
            };
        }

        /// <summary>
        /// 转换字段名
        /// </summary>
        public static string ConvertColumnName(string columnName, ESQLType dbType)
        {
            if (string.IsNullOrEmpty(columnName))
                return columnName;

            return dbType switch
            {
                ESQLType.PostgreSQL => columnName.ToLower(),
                ESQLType.MySQL => columnName,
                _ => columnName
            };
        }

        /// <summary>
        /// 格式化表名（包含引号）
        /// </summary>
        public static string FormatTableName(string tableName, ESQLType dbType)
        {
            var convertedName = ConvertTableName(tableName, dbType);
            
            return dbType switch
            {
                ESQLType.PostgreSQL => convertedName,
                ESQLType.MySQL => $"`{convertedName}`",
                _ => convertedName
            };
        }

        /// <summary>
        /// 格式化字段名（包含引号）
        /// </summary>
        public static string FormatColumnName(string columnName, ESQLType dbType)
        {
            var convertedName = ConvertColumnName(columnName, dbType);
            
            return dbType switch
            {
                ESQLType.PostgreSQL => convertedName,
                ESQLType.MySQL => $"`{convertedName}`",
                _ => convertedName
            };
        }

        /// <summary>
        /// 格式化字段列表（用于INSERT/SELECT）
        /// </summary>
        public static string FormatColumnList(string[] columnNames, ESQLType dbType)
        {
            if (columnNames == null || columnNames.Length == 0)
                return string.Empty;

            var formattedColumns = Array.ConvertAll(columnNames, col => FormatColumnName(col, dbType));
            return string.Join(", ", formattedColumns);
        }

        /// <summary>
        /// 检查表名是否需要转换
        /// </summary>
        public static bool NeedsConversion(ESQLType dbType)
        {
            return dbType == ESQLType.PostgreSQL;
        }
    }
}
