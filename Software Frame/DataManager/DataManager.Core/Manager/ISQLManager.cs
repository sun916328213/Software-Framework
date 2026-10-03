// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ISQLManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL.Definition;

namespace DataManager.Core.SQL;

/// <summary>
/// P12 Core seam：SQL 管理器契约。Controls 只依赖此接口，不持有具体 <c>SQLManager</c>。
/// </summary>
public interface ISQLManager : IManager<ISQL, SQLParam>
{
    /// <summary>创建并注册一个泛型 SQL 实例。</summary>
    ISQL<T> Create<T>(SQLParam param, Action<ISQL<T>>? onCreated = null) where T : class, new();
}
