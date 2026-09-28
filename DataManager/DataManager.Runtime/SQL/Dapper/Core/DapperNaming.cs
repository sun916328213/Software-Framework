using Dommel;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DataManager.SQL.Dapper
{
    /// <summary>
    /// Dapper 命名约定（真库集成测试证明必须存在的原因）：
    /// Dommel 默认复数表名（PgDapperItem → "PgDapperItems"），而本库手写 DDL 用单数，
    /// 两侧约定不一致导致 42P01。改由本库统一：表/列一律小写单数，
    /// 手写 SQL（SqlNamingHelper）与 Dommel（以下解析器）同源，行为一致。
    /// 注意：显式 [Table]/[Column] 名同样会被小写化。
    /// </summary>
    internal sealed class LowerTableNameResolver : ITableNameResolver
    {
        public string ResolveTableName(Type type)
        {
            var name = type.GetCustomAttribute<TableAttribute>()?.Name ?? type.Name;
            return name.ToLowerInvariant();
        }
    }

    internal sealed class LowerColumnNameResolver : IColumnNameResolver
    {
        public string ResolveColumnName(PropertyInfo propertyInfo)
        {
            var name = propertyInfo.GetCustomAttribute<ColumnAttribute>()?.Name ?? propertyInfo.Name;
            return name.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Dommel 全局解析器一次性装配（进程级，只装一次；后装配的测试/业务自定义解析器可覆盖）。
    /// </summary>
    internal static class DapperDommelSetup
    {
        private static bool _done;
        private static readonly object _lock = new();

        public static void Ensure()
        {
            if (_done) return;
            lock (_lock)
            {
                if (_done) return;
                DommelMapper.SetTableNameResolver(new LowerTableNameResolver());
                DommelMapper.SetColumnNameResolver(new LowerColumnNameResolver());
                _done = true;
            }
        }
    }
}
