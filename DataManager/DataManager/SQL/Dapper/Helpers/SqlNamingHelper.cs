using DataManager.SQL.Definition;
using System;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// SQL命名转换助手
    /// 统一约定（与 DapperNaming 注册给 Dommel 的解析器同源）：
    /// 表名/列名一律小写单数，显式 [Table]/[Column] 名同样小写化；
    /// PG 不加引号（fold 后一致），MySQL 加反引号。
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

            return tableName.ToLowerInvariant();
        }

        /// <summary>
        /// 转换字段名
        /// </summary>
        public static string ConvertColumnName(string columnName, ESQLType dbType)
        {
            if (string.IsNullOrEmpty(columnName))
                return columnName;

            return columnName.ToLowerInvariant();
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
        /// 检查表名是否需要转换（当前约定所有库统一小写，恒为 true）
        /// </summary>
        public static bool NeedsConversion(ESQLType dbType)
        {
            return true;
        }
    }
}
