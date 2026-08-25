using DataManager.SQL.EFCore;
using DataManager.SQL.SqlSugarSQL;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataManager.SQL.SqlSugar
{
    public class SqlSugarHelper<T>: SqlSugarBase<T> where T : class , new()
    {
        public static SqlSugarHelper<T> Create()
        {
            var instance = new SqlSugarHelper<T>();
            return instance;
        }

        public static SqlSugarHelper<T> Create(Definition.SQLParam param)
        {
            var instance = Create();
            instance.SQLParam = param;
            instance.Name = param.Name;
            return instance;
        }
    }
}
