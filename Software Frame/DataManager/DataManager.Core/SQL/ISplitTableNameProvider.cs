// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ISplitTableNameProvider。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
namespace DataManager.Core.SQL;

/// <summary>
/// P12 Core seam：分表名查询能力。Dapper / EFCore / SqlSugar 三家实现，
/// Controls 经此接口查询分表，不再对具体基类做 <c>is</c> 类型判断。
/// </summary>
public interface ISplitTableNameProvider
{
    /// <summary>返回当前实例路由到的分表名集合。</summary>
    List<string> GetSplitTables();
}
