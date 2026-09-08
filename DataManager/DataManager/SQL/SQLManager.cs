using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;
using DataManager.SQL.SqlSugarSQL;
using System;
using System.Collections.Generic;

namespace DataManager.SQL
{
    /// <summary>
    /// SQL 管理器（已去 Prism 容器依赖，P0-2）。
    /// 需要注册到 IOC 时，由上层（DataManagerModule/Workbench）显式注册，不再由 Manager 隐式操作容器。
    /// </summary>
    public class SQLManager : ManagerBase<ISQL, SQLParam>
    {
        private readonly Action<object>? _onCreated;

        public SQLManager()
        {
        }

        /// <summary>
        /// 可选：创建实例后的回调（上层可用于 RegisterInstance 到容器）。
        /// </summary>
        public SQLManager(Action<object> onCreated)
        {
            _onCreated = onCreated;
        }
 
        public override void Config(List<SQLParam> param)
        {
            foreach (var item in param)
                Create(item);
        }

        public ISQL<T> Create<T>(SQLParam param, Action<ISQL<T>>? onCreated = null) where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(param);
            param.Validate();
            if (Contains(param.Name))
                throw new InvalidOperationException($"已存在同名数据库实例: {param.Name}");
            ISQL<T> sql = param.ToolType switch
            {
                ESQLToolType.EFCore => EFCoreHelper<T>.Create(param),
                ESQLToolType.SqlSugar => SqlSugarHelper<T>.Create(param),
                ESQLToolType.Dapper => DapperHelper<T>.Create(param),
                _ => throw new NotSupportedException($"{param.ToolType} not support.")
            };

            Add(sql);
            _onCreated?.Invoke(sql);
            onCreated?.Invoke(sql);

            return sql;
        }

        public override ISQL Create(SQLParam param)
        {
            throw new NotSupportedException("Please use Create<T> method instead.");
        }

        protected override string GetName(ISQL device)
        {
            return device.SQLParam.Name;
        }
    }
}
