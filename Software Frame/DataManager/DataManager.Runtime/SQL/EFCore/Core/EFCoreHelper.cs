// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：EFCoreHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
namespace DataManager.Runtime.SQL.EFCore
{
    /// <summary>
    /// EF Core 数据库帮助类（P1-2：删除 Reflection.Emit 动态类型）。
    /// EF 的模型缓存以闭合泛型为键，EFCoreHelper&lt;T&gt; 本身已按 T 隔离，无需动态子类。
    /// </summary>
    public class EFCoreHelper<T> : EFCoreBase<T> where T : class
    {
        /// <summary>
        /// 初始化 EFCoreHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public EFCoreHelper() { }

        /// <summary>
        /// 初始化 EFCoreHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public EFCoreHelper(global::DataManager.Core.SQL.Definition.SQLParam param) : base(param) { }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// </summary>
        public static EFCoreHelper<T> Create()
        {
            return new EFCoreHelper<T>();
        }

        /// <summary>
        /// 创建 EFCoreHelper 实例并设置 SQLParam
        /// </summary>
        public static EFCoreHelper<T> Create(global::DataManager.Core.SQL.Definition.SQLParam param)
        {
            return new EFCoreHelper<T>(param);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ForkForTable。
        /// </summary>
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
