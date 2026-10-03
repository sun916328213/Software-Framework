// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ILogManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Log.Definition;

namespace DataManager.Core.Log;

/// <summary>
/// P12 Core seam：日志管理器契约。Controls 只依赖此接口，不持有具体 <c>LogManager</c>。
/// </summary>
public interface ILogManager : IManager<ILog, LogParam>
{
    /// <summary>
    /// 原子替换调用方拥有的日志集合；构造或名称校验失败时保留全部旧实例。
    /// </summary>
    void ReplaceConfigured(
        IReadOnlyCollection<string> ownedNames,
        IReadOnlyList<LogParam> parameters);
}
