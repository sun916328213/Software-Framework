// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ILogHistoryReader、ILogHistoryQueryService、LogHistorySummary、LogHistoryQueryService。成员说明可结合下方具体实现阅读。
namespace WPFControls.Queries.Logs;

/// <summary>
/// ILogHistoryReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface ILogHistoryReader<T>
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// </summary>
    Task<IEnumerable<T>> ReadAsync(DateTime start, DateTime end);
}

/// <summary>
/// ILogHistoryQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface ILogHistoryQueryService<T>
{
    /// <summary>
    /// 执行查询操作；对应入口为 QueryAsync。
    /// </summary>
    Task<IReadOnlyList<T>> QueryAsync(DateTime start, DateTime end);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Filter。
    /// </summary>
    IReadOnlyList<T> Filter(IReadOnlyList<T> rows, string? field, string? text);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Select。
    /// </summary>
    IReadOnlyList<T> Select(IReadOnlyList<T> rows, Func<T, bool> predicate);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Page。
    /// </summary>
    IReadOnlyList<T> Page(IReadOnlyList<T> rows, int page, int size);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Summarize。
    /// </summary>
    IReadOnlyList<LogHistorySummary> Summarize(IReadOnlyList<T> rows, Func<T, bool> include, Func<T, string> info);
}

/// <summary>
/// LogHistorySummary 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public sealed record LogHistorySummary(string Info, int Count);

/// <summary>Stateless query, filtering and paging. The caller supplies the existing level and message mapping.</summary>
/// <remarks>
/// LogHistoryQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// 继承或实现的契约：ILogHistoryQueryService&lt;T&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class LogHistoryQueryService<T>(ILogHistoryReader<T> reader) : ILogHistoryQueryService<T>
{
    /// <summary>
    /// 执行查询操作；对应入口为 QueryAsync。
    /// 正文中的关键协作调用包括 AsReadOnly、ToArray、ConfigureAwait、ReadAsync、AddDays；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task<IReadOnlyList<T>> QueryAsync(DateTime start, DateTime end) =>
        Array.AsReadOnly((await reader.ReadAsync(start, end.AddDays(1)).ConfigureAwait(false)).ToArray());

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Filter。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、GetProperty、AsReadOnly、ToArray、Where；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<T> Filter(IReadOnlyList<T> rows, string? field, string? text)
    {
        var property = string.IsNullOrEmpty(field) ? null : typeof(T).GetProperty(field);
        return property == null || string.IsNullOrEmpty(text) ? Array.AsReadOnly(rows.ToArray()) :
            Array.AsReadOnly(rows.Where(row => property.GetValue(row)?.ToString() is { } value &&
                value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToArray());
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Select。
    /// 正文中的关键协作调用包括 AsReadOnly、ToArray、Where；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<T> Select(IReadOnlyList<T> rows, Func<T, bool> predicate) => Array.AsReadOnly(rows.Where(predicate).ToArray());

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Page。
    /// 正文中的关键协作调用包括 AsReadOnly、ToArray、Take、Skip；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<T> Page(IReadOnlyList<T> rows, int page, int size) =>
        Array.AsReadOnly(rows.Skip((page - 1) * (size < 1 ? 1 : size)).Take(size < 1 ? 1 : size).ToArray());

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Summarize。
    /// 正文中的关键协作调用包括 AsReadOnly、ToArray、Select、GroupBy、Where；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<LogHistorySummary> Summarize(IReadOnlyList<T> rows, Func<T, bool> include, Func<T, string> info) =>
        Array.AsReadOnly(rows.Where(include).GroupBy(info).Select(group => new LogHistorySummary(group.Key, group.Count())).ToArray());
}
