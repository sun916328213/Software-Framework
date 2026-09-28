using System;

namespace DataManager.SQL.Definition
{
    /// <summary>
    /// SQL命名转换助手
    /// P15：从 DataManager.SQL.Dapper 搬到 Definition——命名约定是全工具共享的（手写 SQL 与 Dommel/EF/SqlSugar 映射同源）。
    /// 统一约定：表名/列名一律小写单数，显式 [Table]/[Column] 名同样小写化；
    /// 引号规则按方言（PG 无引号，MySQL 反引号，SQLite 双引号，SQL Server 中括号）。
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
        /// 格式化表名（包含引号）P3-2：转调方言 Quote。
        /// </summary>
        public static string FormatTableName(string tableName, ESQLType dbType)
        {
            if (string.IsNullOrEmpty(tableName))
                return tableName;
            return SqlDialectRegistry.Get(dbType).QuoteTable(tableName);
        }

        /// <summary>
        /// 格式化字段名（包含引号）
        /// </summary>
        public static string FormatColumnName(string columnName, ESQLType dbType)
        {
            if (string.IsNullOrEmpty(columnName))
                return columnName;
            return SqlDialectRegistry.Get(dbType).QuoteColumn(columnName);
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
