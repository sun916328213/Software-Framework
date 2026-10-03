// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ITableQueryReader、ITableQueryService、TableQueryService。成员说明可结合下方具体实现阅读。
namespace WPFControls.Queries.Data;

/// <summary>
/// ITableQueryReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface ITableQueryReader<T>
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 Read。
    /// </summary>
    IEnumerable<T>? Read(DateTime start, DateTime end, string? id);
}

/// <summary>
/// ITableQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public interface ITableQueryService<T>
{
    /// <summary>
    /// 执行查询操作；对应入口为 Query。
    /// </summary>
    IReadOnlyList<T> Query(DateTime start, DateTime end, string? id);
}

/// <summary>Preserves the existing inclusive next-day boundary; owns no page state.</summary>
/// <remarks>
/// TableQueryService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// 继承或实现的契约：ITableQueryService&lt;T&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class TableQueryService<T>(ITableQueryReader<T> reader) : ITableQueryService<T>
{
    /// <summary>
    /// 执行查询操作；对应入口为 Query。
    /// 正文中的关键协作调用包括 AsReadOnly、ToArray、Read、AddDays；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<T> Query(DateTime start, DateTime end, string? id) =>
        Array.AsReadOnly((reader.Read(start.Date, end.AddDays(1).Date, id) ?? []).ToArray());
}
