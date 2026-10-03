// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：SqlTableQueryReader、RunLogHistoryReader、SemiLogHistoryReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.Log、DataManager.Core.Log.Definition、DataManager.Core.SQL、DataManager.Runtime.SQL、WPFControls.Queries.Data、WPFControls.Queries.Logs。导入不代表所有类型都会在运行时加载。
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using WPFControls.Queries.Data;
using WPFControls.Queries.Logs;
using WPFControls.ProgramControl.Models;

namespace WPFControls.Integration.Queries;

/// <summary>
/// SqlTableQueryReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：ITableQueryReader&lt;T&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class SqlTableQueryReader<T>(SQLManager manager) : ITableQueryReader<T> where T : ISQLDataBase
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 Read。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、Query；具体条件和先后次序见方法体。
    /// </summary>
    public IEnumerable<T>? Read(DateTime start, DateTime end, string? id)
    {
        var sql = (ISQL<T>)manager[0];
        return string.IsNullOrEmpty(id)
            ? sql.Query(row => row.Time.Date >= start && row.Time.Date <= end)
            : sql.Query(row => row.Time.Date >= start && row.Time.Date <= end && row.ID == id);
    }
}


/// <summary>
/// RunLogHistoryReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：ILogHistoryReader&lt;LogInfo&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class RunLogHistoryReader(ILogManager manager) : ILogHistoryReader<LogInfo>
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// 正文中的关键协作调用包括 QueryAsync、TryGet；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task<IEnumerable<LogInfo>> ReadAsync(DateTime start, DateTime end) =>
        await (manager.TryGet("RunLog", out var log) ? log : null)!.QueryAsync(start, end);
}

/// <summary>
/// SemiLogHistoryReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：ILogHistoryReader&lt;string&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class SemiLogHistoryReader(ILogManager manager) : ILogHistoryReader<string>
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// 正文中的关键协作调用包括 Query_MultiAsync、TryGet；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task<IEnumerable<string>> ReadAsync(DateTime start, DateTime end) =>
        await (manager.TryGet("SEMILog", out var log) ? log : null)!.Query_MultiAsync(start, end);
}
