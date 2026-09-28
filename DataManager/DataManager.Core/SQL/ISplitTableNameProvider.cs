namespace DataManager.SQL;

/// <summary>
/// P12 Core seam：分表名查询能力。Dapper / EFCore / SqlSugar 三家实现，
/// Controls 经此接口查询分表，不再对具体基类做 <c>is</c> 类型判断。
/// </summary>
public interface ISplitTableNameProvider
{
    /// <summary>返回当前实例路由到的分表名集合。</summary>
    List<string> GetSplitTables();
}
