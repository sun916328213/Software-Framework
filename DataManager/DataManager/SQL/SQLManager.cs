using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;
using DataManager.SQL.SqlSugarSQL;
using Prism.Ioc;
using System;
using System.Collections.Generic;

namespace DataManager.SQL
{
    /// <summary>
    /// SQL 管理器
    /// </summary>
    public class SQLManager : ManagerBase<ISQL, SQLParam>
    {
        IContainerExtension ContainerRegistry;
        public SQLManager(IContainerExtension container )
        {
            ContainerRegistry = container;
        }
 
        public override void Config(List<SQLParam> param)
        {
            foreach (var item in param)
                Create(item);
        }

        public ISQL<T> Create<T>(SQLParam param) where T : class, new()
        {
            ISQL<T> sql = param.ToolType switch
            {
                ESQLToolType.EFCore => EFCoreHelper<T>.Create(param),
                ESQLToolType.SqlSugar => SqlSugarHelper<T>.Create(param),
                ESQLToolType.Dapper => DapperHelper<T>.Create(param),
                _ => throw new NotSupportedException($"{param.ToolType} not support.")
            };

            DataManagers.Add(sql);
            // 将实例注册为 IOC 单例
            ContainerRegistry.RegisterInstance (typeof(ISQL<T>), sql);
            
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
