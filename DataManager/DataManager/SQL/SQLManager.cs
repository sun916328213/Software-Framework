using DataManager.Log.Definition;
using DataManager.SQL.Dapper;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugar;
using DataManager.SQL.SqlSugarSQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.SQL
{
    public class SQLManager : ManagerBase<ISQL, SQLParam>
    {
        public override void Config(List<SQLParam> param)
        {
            foreach (var item in param)
                Create(item);
        }

        public ISQL<T> Create<T>(SQLParam param) where T : class, new()
        {
            switch (param.ToolType)
            {
                case ESQLToolType.EFCore:
                    var efc = EFCoreHelper<T>.Create(param);
                    DataManagers.Add(efc);
                    return efc;
                case ESQLToolType.SqlSugar:
                    var sugar = SqlSugarHelper<T>.Create(param);
                    DataManagers.Add(sugar);
                    return sugar;
                case ESQLToolType.Dapper:
                    var dapper = DapperHelper<T>.Create(param);
                    DataManagers.Add(dapper);
                    return dapper;
                default: throw new NotSupportedException($"{param.ToolType} not support.");
            }
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
