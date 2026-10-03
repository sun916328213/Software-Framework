// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LowerTableNameResolver、LowerColumnNameResolver、DapperDommelSetup。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、Dommel、System。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using Dommel;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DataManager.Runtime.SQL.Dapper
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
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ResolveTableName。
        /// 正文中的关键协作调用包括 GetCustomAttribute、ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
        public string ResolveTableName(Type type)
        {
            var name = type.GetCustomAttribute<TableAttribute>()?.Name ?? type.Name;
            return name.ToLowerInvariant();
        }
    }

    /// <summary>
    /// LowerColumnNameResolver 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// 继承或实现的契约：IColumnNameResolver。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal sealed class LowerColumnNameResolver : IColumnNameResolver
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ResolveColumnName。
        /// 正文中的关键协作调用包括 GetCustomAttribute、ToLowerInvariant；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Ensure。
        /// 正文中的关键协作调用包括 SetTableNameResolver、SetColumnNameResolver；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
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
