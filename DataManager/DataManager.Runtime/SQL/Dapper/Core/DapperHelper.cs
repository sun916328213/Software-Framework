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
            return new DapperHelper<T>(param);
        }

        public DapperHelper()
        {
        }

        public DapperHelper(Definition.SQLParam param) : base(param)
        {
        }
    }
}
