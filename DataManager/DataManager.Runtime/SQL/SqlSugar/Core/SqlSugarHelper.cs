using DataManager.SQL.SqlSugarSQL;

namespace DataManager.SQL.SqlSugar
{
    public class SqlSugarHelper<T>: SqlSugarBase<T> where T : class , new()
    {
        public SqlSugarHelper()
        {
        }

        public SqlSugarHelper(Definition.SQLParam param) : base(param)
        {
        }

        public static SqlSugarHelper<T> Create()
        {
            var instance = new SqlSugarHelper<T>();
            return instance;
        }

        public static SqlSugarHelper<T> Create(Definition.SQLParam param)
        {
            return new SqlSugarHelper<T>(param);
        }
    }
}
