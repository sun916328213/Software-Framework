// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：SqlTableQueryReader、RunLogHistoryReader、SemiLogHistoryReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.Log、DataManager.Core.Log.Definition、DataManager.Core.SQL、WPFControls.Queries.Data、WPFControls.Queries.Logs。导入不代表所有类型都会在运行时加载。
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using DataManager.Core.SQL;
using WPFControls.Queries.Data;
using WPFControls.Queries.Logs;
using WPFControls.ProgramControl.Models;

namespace WPFControls.Integration.Queries;

/// <summary>
/// SqlTableQueryReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：ITableQueryReader&lt;T&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
/// <remarks>
/// <para><b>T11：实体身份不再靠注册序号。</b>改造前的实现是 <c>(ISQL&lt;T&gt;)manager[0]</c> ——
/// 实体/数据库身份由「谁先被 <c>Create</c>」决定，因此</para>
/// <list type="bullet">
/// <item>换注册顺序就会强转失败（例如第二个实体被排到 0 号）；</item>
/// <item>要求宿主必须提供**具体** <c>SQLManager</c>，模块却允许替换 <see cref="ISQLManager"/>；</item>
/// <item>同实体多连接时无法表达「要哪一个」。</item>
/// </list>
/// <para><b>现在只依赖 Core 契约</b> <see cref="ISQL{T}"/>：宿主把每个查询实体的
/// <c>ISQL&lt;T&gt;</c> 注册进容器（见 MachineApp 的 <c>SqlInstanceComposition</c>），
/// 这里按**类型**惰性取得。</para>
/// <para><b>为什么必须惰性</b>（<see cref="Func{TResult}"/> 而不是直接注入 <c>ISQL&lt;T&gt;</c>）：
/// 容器注册阶段数据库尚未配置，此时创建实例既会失败也会抢在配置之前；
/// 惰性获取把「取实例」推迟到真正查询的时刻。这与 <c>PackingPersistenceComposition</c>
/// 里 <c>() =&gt; provider.Resolve&lt;ISQL&lt;CapacityStatisticsInfo&gt;&gt;()</c> 的既有形态一致。</para>
/// <para>查不到实例时**明确失败**（抛 <see cref="InvalidOperationException"/>），
/// 不静默回退到「某个碰巧存在的实例」——那正是改造前 <c>[0]</c> 的问题。</para>
/// </remarks>
internal sealed class SqlTableQueryReader<T>(Func<ISQL<T>> getSql) : ITableQueryReader<T> where T : ISQLDataBase
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 Read。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、Query；具体条件和先后次序见方法体。
    /// </summary>
    public IEnumerable<T>? Read(DateTime start, DateTime end, string? id)
    {
        var sql = getSql() ?? throw new InvalidOperationException(
            $"未取得 {typeof(T).FullName} 的 SQL 实例：宿主必须为该查询实体注册 ISQL<{typeof(T).Name}>。");
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
