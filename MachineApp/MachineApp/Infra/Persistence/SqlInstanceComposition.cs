// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：SqlInstanceComposition。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.SQL、MachineApp.Infra.Persistence.Entities、Prism.Ioc、System、System.Linq。导入不代表所有类型都会在运行时加载。
using DataManager.Core.SQL;
using MachineApp.Infra.Persistence.Entities;
using Prism.Ioc;
using System;
using System.Linq;

namespace MachineApp.Infra.Persistence;

/// <summary>
/// T11：把机型宿主真正查询/写入的 SQL 实体，按**类型**暴露成容器里的 <c>ISQL&lt;T&gt;</c>。
/// </summary>
/// <remarks>
/// <para><b>改造前的问题</b>：生产查询路径的「身份」是 <c>SQLManager</c> 的**注册序号** ——
/// <c>SqlTableQueryReader&lt;T&gt;</c> 里写死 <c>(ISQL&lt;T&gt;)manager[0]</c>，
/// <c>UnLoad.AddSQL</c> 写死 <c>(ISQL&lt;CSQLData&gt;)_SQLManager[0]</c>，
/// <c>DbTestManager</c> 写死 <c>_sqlManager[0] as ISQL&lt;CSQLData&gt;</c>。
/// 于是「谁是 0 号」由 <c>02_初始化工具</c> 里 <c>Create</c> 的先后决定：
/// 换注册顺序、加第二个实体、或把 <c>ISQLManager</c> 换成自定义实现，都会解析或强转失败。</para>
/// <para><b>现在的选择方式</b>：按类型取（<see cref="Require{TEntity}"/>），并且**惰性** ——
/// 容器注册阶段数据库还没配置（<c>02_初始化工具</c> 才 <c>Create</c> + <c>Connect</c>），
/// 提前取既会失败也会抢在配置之前。惰性形态与
/// <see cref="PackingPersistenceComposition"/> 里 <c>() =&gt; provider.Resolve&lt;ISQL&lt;...&gt;&gt;()</c> 一致。</para>
/// <para>取不到、或同类型有多个候选时**明确失败**，不退回「某个碰巧存在的实例」。</para>
/// </remarks>
internal static class SqlInstanceComposition
{
    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 Register。
    /// 正文中的关键协作调用包括 RegisterInstance；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Register(IContainerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        RegisterInstance<CSQLData>(registry);
        RegisterInstance<CapacityStatisticsInfo>(registry);
    }

    /// <summary>
    /// 为单个实体登记惰性工厂。宿主若已自行提供 <c>ISQL&lt;T&gt;</c> 则不覆盖 ——
    /// 与 <see cref="PackingPersistenceComposition"/> 的替换约定保持一致，
    /// 避免模块/宿主的注册顺序把替换静默盖掉。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么是 transient 而不是 singleton</b>（实测踩到）：</para>
    /// <list type="bullet">
    /// <item>DryIoc 的 <c>RegisterSingleton</c> 工厂**一旦抛过一次异常，该服务就被标记为失败**：
    /// 之后再解析不会重跑工厂，而是等 5 秒后抛
    /// <c>Error.WaitForScopedServiceIsCreatedTimeoutExpired</c>。而这里的工厂在
    /// 「数据库还没 Create」时**本来就该抛** ⇒ 一次过早的查询会把这个服务永久废掉，
    /// 直到进程重启。改造前的 <c>manager[0]</c> 是每次 Read 现取，没有这个问题。</item>
    /// <item>transient 每次解析重跑工厂 ⇒ 天生自愈，并且**与改造前语义一致**：
    /// 每次都取管理器**当前**的实例（档案切换重建实例后，读者立刻看到新的那个）。</item>
    /// <item>生命周期本来就归 <see cref="ISQLManager"/> 所有（<c>Create</c>/<c>Remove</c>/<c>Dispose</c>），
    /// 容器不该替它宣称所有权，也不该替它释放。</item>
    /// </list>
    /// </remarks>
    private static void RegisterInstance<TEntity>(IContainerRegistry registry)
    {
        if (!registry.IsRegistered<ISQL<TEntity>>())
            registry.Register<ISQL<TEntity>>(
                provider => Require<TEntity>(provider.Resolve<ISQLManager>()));
    }

    /// <summary>
    /// 按**类型**从管理器取唯一实例。取不到、或同类型有多个候选，都抛明确异常 ——
    /// 不退回「某个碰巧存在的实例」（那正是改造前索引 0 的隐患）。
    /// </summary>
    /// <param name="manager">宿主注册的 SQL 管理器（Core 契约类型，允许自定义实现）。</param>
    /// <param name="name">可选：同类型多实例时，按 <c>ISQL.SQLParam.Name</c> 显式指定要哪一个。</param>
    internal static ISQL<TEntity> Require<TEntity>(ISQLManager manager, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        var candidates = manager.Items.OfType<ISQL<TEntity>>().ToArray();
        var matches = candidates
            .Where(item => name is null || string.Equals(item.SQLParam.Name, name, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 1)
            return matches[0];

        var scope = name is null ? "未指定名称" : $"名称「{name}」";
        var known = candidates.Length == 0
            ? "无"
            : string.Join("、", candidates.Select(item => item.SQLParam.Name));

        throw new InvalidOperationException(matches.Length == 0
            ? $"未找到 {typeof(TEntity).FullName} 的 SQL 实例（{scope}）；已注册的同类型实例：{known}。"
            : $"{typeof(TEntity).FullName} 有 {matches.Length} 个候选实例（{scope}），无法确定要哪一个；"
              + $"请用名称显式指定。候选名称：{string.Join("、", matches.Select(item => item.SQLParam.Name))}。");
    }
}
