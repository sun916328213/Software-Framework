using DataManager.SQL.Definition;

namespace DataManager.SQL;

/// <summary>
/// P12 Core seam：SQL 管理器契约。Controls 只依赖此接口，不持有具体 <c>SQLManager</c>。
/// </summary>
public interface ISQLManager : IManager<ISQL, SQLParam>
{
    /// <summary>创建并注册一个泛型 SQL 实例。</summary>
    ISQL<T> Create<T>(SQLParam param, Action<ISQL<T>>? onCreated = null) where T : class, new();
}
