// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：IManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using System.Collections.Generic;

namespace DataManager.Core
{
    /// <summary>
    /// 管理器抽象（P1-1）：统一 Config/Create/Add/Remove/Clear/TryGet 契约。
    /// </summary>
    public interface IManager<T, TParam>
    {
        IReadOnlyList<T> Items { get; }
        int Count { get; }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Config。
        /// </summary>
        void Config(List<TParam> param);
        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// </summary>
        T Create(TParam param);
        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// </summary>
        void Add(T item);
        /// <summary>
        /// 执行移除操作；对应入口为 Remove。
        /// </summary>
        bool Remove(T item);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Clear。
        /// </summary>
        void Clear();
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGet。
        /// </summary>
        bool TryGet(string name, out T? item);
    }
}
