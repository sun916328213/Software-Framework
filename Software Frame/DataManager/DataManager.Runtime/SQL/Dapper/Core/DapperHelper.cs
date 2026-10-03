// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：DapperHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
namespace DataManager.Runtime.SQL.Dapper
{
    /// <summary>
    /// DapperHelper 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// 继承或实现的契约：DapperBase&lt;T&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class DapperHelper<T>: DapperBase<T> where T : class , new()
    {
        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// </summary>
        public static DapperHelper<T> Create()
        {
            var instance = new DapperHelper<T>();
            return instance;
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// </summary>
        public static DapperHelper<T> Create(global::DataManager.Core.SQL.Definition.SQLParam param)
        {
            return new DapperHelper<T>(param);
        }

        /// <summary>
        /// 初始化 DapperHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DapperHelper()
        {
        }

        /// <summary>
        /// 初始化 DapperHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DapperHelper(global::DataManager.Core.SQL.Definition.SQLParam param) : base(param)
        {
        }
    }
}
