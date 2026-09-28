namespace DataManager.SQL.EFCore
{
    /// <summary>
    /// EF Core 数据库帮助类（P1-2：删除 Reflection.Emit 动态类型）。
    /// EF 的模型缓存以闭合泛型为键，EFCoreHelper&lt;T&gt; 本身已按 T 隔离，无需动态子类。
    /// </summary>
    public class EFCoreHelper<T> : EFCoreBase<T> where T : class
    {
        public EFCoreHelper() { }

        public EFCoreHelper(Definition.SQLParam param) : base(param) { }

        public static EFCoreHelper<T> Create()
        {
            return new EFCoreHelper<T>();
        }

        /// <summary>
        /// 创建 EFCoreHelper 实例并设置 SQLParam
        /// </summary>
        public static EFCoreHelper<T> Create(Definition.SQLParam param)
        {
            return new EFCoreHelper<T>(param);
        }

        public override EFCoreBase<T> ForkForTable(string tableName)
        {
            var fork = new EFCoreHelper<T>
            {
                SQLParam = SQLParam,
                Name = Name,
                SplitTableName = tableName,
            };
            return fork;
        }
    }
}
