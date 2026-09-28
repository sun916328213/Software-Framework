using DataManager.Log.Definition;

namespace DataManager.Log;

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
