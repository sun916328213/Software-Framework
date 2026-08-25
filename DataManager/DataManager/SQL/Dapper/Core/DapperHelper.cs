using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugarSQL;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataManager.SQL.Dapper
{
    public class DapperHelper<T>: DapperBase<T> where T : class , new()
    {
        public static DapperHelper<T> Create()
        {
            var instance = new DapperHelper<T>();
            return instance;
        }

        public static DapperHelper<T> Create(Definition.SQLParam param)
        {
            var instance = Create();
            instance.SQLParam = param;
            instance.Name = param.Name;
            return instance;
        }
    }
}
