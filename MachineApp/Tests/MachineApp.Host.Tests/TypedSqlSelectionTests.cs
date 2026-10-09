// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TypedSqlSelectionTests、TypedRow、AlphaRow、BetaRow、SqlInstanceProbe、FakeSqlManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Reflection、System.ComponentModel.DataAnnotations、DataManager.Core.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL、MachineApp.Infra.Persistence、MachineApp.Infra.Persistence.Entities、Prism.DryIoc、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DataManager.Core.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL;
using MachineApp.Infra.Persistence;
using MachineApp.Infra.Persistence.Entities;
using Prism.DryIoc;
using Prism.Ioc;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// T11 专项：生产 SQL 选择必须**按类型**而不是**按注册序号**。
/// </summary>
/// <remarks>
/// <para><b>被锁死的旧行为</b>（A07）：<c>SqlTableQueryReader&lt;T&gt;</c> 用 <c>(ISQL&lt;T&gt;)manager[0]</c>，
/// <c>UnLoad</c> 用 <c>(ISQL&lt;CSQLData&gt;)_SQLManager[0]</c> —— 实体身份 = 谁先被 <c>Create</c>。
/// 换注册顺序、加一个实体、或把 <c>ISQLManager</c> 换成自定义实现，就会解析/强转失败。</para>
/// <para><b>本类覆盖</b>：逆序注册、自定义 <c>ISQLManager</c>、缺配置明确失败、
/// 同实体多连接的显式选择、容器登记的惰性与宿主替换、以及真实 SQLite 实例的按类型解析。</para>
/// <para>不用 <c>ProductionFixture</c>（不启动整机 App）：这里的断言都落在
/// <c>SqlInstanceComposition</c> + 容器登记上，与 UI/启动脚本无关，跑起来快且确定。
/// 端到端的宿主查询路径由 <c>QueryArchitectureHostTests</c> 覆盖。</para>
/// </remarks>
public sealed class TypedSqlSelectionTests
{
    /// <summary>
    /// 逆序注册：后创建的实体排在 0 号位，按类型仍然各取各的 ——
    /// 旧实现会拿 0 号位强转成 <see cref="AlphaRow"/> 而抛 <see cref="InvalidCastException"/>。
    /// </summary>
    [Fact]
    public void ReverseRegistrationOrder_IsSelectedByTypeNotByIndex()
    {
        var manager = new FakeSqlManager();
        var alpha = Probe<AlphaRow>("alpha");
        var beta = Probe<BetaRow>("beta");
        // 故意逆序：Beta 落在 0 号位。
        manager.Add(beta);
        manager.Add(alpha);

        Assert.Equal(beta, manager.Items[0]);
        Assert.Same(alpha, SqlInstanceComposition.Require<AlphaRow>(manager));
        Assert.Same(beta, SqlInstanceComposition.Require<BetaRow>(manager));
    }

    /// <summary>
    /// 自定义 <c>ISQLManager</c> 实现（不是具体 <c>SQLManager</c>）同样可用 ——
    /// 旧实现要求 reader 拿到具体类，与「模块允许替换 <c>ISQLManager</c>」的契约相矛盾。
    /// </summary>
    [Fact]
    public void CustomManagerImplementation_ReplacesConcreteSqlManager()
    {
        var manager = new FakeSqlManager();
        Assert.IsNotType<SQLManager>(manager);
        var alpha = Probe<AlphaRow>("alpha");
        manager.Add(alpha);

        Assert.Same(alpha, SqlInstanceComposition.Require<AlphaRow>(manager));
    }

    /// <summary>
    /// 缺配置时**明确失败**，且消息里带实体类型名（不退回「某个碰巧存在的实例」）。
    /// </summary>
    [Fact]
    public void MissingInstance_FailsWithEntityNameInMessage()
    {
        var manager = new FakeSqlManager();
        manager.Add(Probe<BetaRow>("beta"));

        var error = Assert.Throws<InvalidOperationException>(() => SqlInstanceComposition.Require<AlphaRow>(manager));
        Assert.Contains(typeof(AlphaRow).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains("未找到", error.Message, StringComparison.Ordinal);
        // 已注册的同类型实例清单必须写进消息，方便定位（这里是「无」）。
        Assert.Contains("已注册的同类型实例：无", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 同一实体、不同连接：不猜，必须按名称显式指定；不指定就报错并列出候选名。
    /// </summary>
    [Fact]
    public void SameEntityWithTwoConnections_RequiresExplicitName()
    {
        var manager = new FakeSqlManager();
        var connectionA = Probe<AlphaRow>("conn-a");
        var connectionB = Probe<AlphaRow>("conn-b");
        manager.Add(connectionA);
        manager.Add(connectionB);

        Assert.Same(connectionA, SqlInstanceComposition.Require<AlphaRow>(manager, "conn-a"));
        Assert.Same(connectionB, SqlInstanceComposition.Require<AlphaRow>(manager, "conn-b"));

        var error = Assert.Throws<InvalidOperationException>(() => SqlInstanceComposition.Require<AlphaRow>(manager));
        Assert.Contains("无法确定要哪一个", error.Message, StringComparison.Ordinal);
        Assert.Contains("conn-a", error.Message, StringComparison.Ordinal);
        Assert.Contains("conn-b", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 容器登记的惰性、自愈与替换语义：
    /// 注册阶段（数据库还没配置）解析必须**失败**而不是拿到半成品；
    /// 失败过之后，配置完成时同一解析点必须**立刻可用**（自愈 —— 锁住 transient 注册）；
    /// 宿主已自行提供 <c>ISQL&lt;T&gt;</c> 时不被默认实现覆盖。
    /// </summary>
    /// <remarks>
    /// 「自愈」这条不是凑数：DryIoc 的 <c>RegisterSingleton</c> 工厂**一旦抛过一次异常**，
    /// 该服务就被标记为失败，之后再解析不会重跑工厂，而是等 5 秒后抛
    /// <c>Error.WaitForScopedServiceIsCreatedTimeoutExpired</c>。
    /// 把注册改成 singleton，本用例会直接红。
    /// </remarks>
    [Fact]
    public void ContainerRegistration_IsLazySelfHealingAndKeepsHostReplacement()
    {
        var manager = new SQLManager();
        var container = new DryIocContainerExtension();
        container.RegisterInstance<ISQLManager>(manager);
        SqlInstanceComposition.Register(container);

        // 惰性：此时管理器里什么都没有（相当于 02_初始化工具 还没跑）。
        var missing = Assert.ThrowsAny<Exception>(() => container.Resolve<ISQL<CSQLData>>());
        Assert.Contains(typeof(CSQLData).FullName!, missing.Message, StringComparison.Ordinal);

        // 自愈：「初始化」完成后，同一个解析点立刻可用，且每次都拿到管理器当前的那个实例。
        var created = Probe<CSQLData>("main");
        manager.Add(created);
        Assert.Same(created, container.Resolve<ISQL<CSQLData>>());
        Assert.Same(created, container.Resolve<ISQL<CSQLData>>());

        // 宿主替换优先：已注册的 ISQL<T> 不被默认工厂盖掉。
        var replacement = Probe<CSQLData>("host-owned");
        var hostContainer = new DryIocContainerExtension();
        hostContainer.RegisterInstance<ISQL<CSQLData>>(replacement);
        SqlInstanceComposition.Register(hostContainer);
        Assert.Same(replacement, hostContainer.Resolve<ISQL<CSQLData>>());
    }

    /// <summary>
    /// 真实 SQLite + DTO：按类型登记的实例能真的连库、写入、读回；
    /// 并且把另一个实体的实例放在 0 号位也不影响结果。
    /// </summary>
    [Fact]
    public void RealSqliteInstance_IsSelectedByTypeAndReadsBack()
    {
        var file = Path.Combine(Path.GetTempPath(), $"t11_typed_sql_{Guid.NewGuid():N}.db");
        var manager = new SQLManager();
        var other = Probe<BetaRow>("beta");
        manager.Add(other);

        var db = manager.Create<TypedRow>(new SQLParam
        {
            Name = "typed-sqlite",
            FilePath = file,
            DBName = file,
            DBType = ESQLType.SQLite,
            ToolType = ESQLToolType.EFCore,
        });
        try
        {
            db.Connect();
            db.Add(new TypedRow { Id = 1, Name = "alpha", Time = new DateTime(2026, 10, 1) });

            var container = new DryIocContainerExtension();
            container.RegisterInstance<ISQLManager>(manager);
            container.RegisterSingleton<ISQL<TypedRow>>(_ => SqlInstanceComposition.Require<TypedRow>(manager));

            var resolved = container.Resolve<ISQL<TypedRow>>();
            Assert.Same(db, resolved);
            var row = Assert.Single(resolved.Query());
            Assert.Equal("alpha", row.Name);
            Assert.Equal(new DateTime(2026, 10, 1), row.Time);
        }
        finally
        {
            db.DisConnect();
            db.Dispose();
            try { File.Delete(file); } catch { /* 临时文件清理失败不影响断言 */ }
        }
    }

    /// <summary>创建一个带名字的 SQL 探针（<see cref="DispatchProxy"/> 生成的实现）。</summary>
    private static ISQL<T> Probe<T>(string name)
    {
        var proxy = DispatchProxy.Create<ISQL<T>, SqlInstanceProbe<T>>();
        ((SqlInstanceProbe<T>)(object)proxy).Name = name;
        return proxy;
    }

    /// <summary>最小 SQL 行，仅用于真实 SQLite 往返。</summary>
    public sealed class TypedRow
    {
        [Key]
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Time { get; set; }
    }

    /// <summary>类型化选择用的实体 A。</summary>
    public sealed class AlphaRow
    {
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>类型化选择用的实体 B。</summary>
    public sealed class BetaRow
    {
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// SQL 探针：只实现选择逻辑用到的成员（<c>SQLParam</c> 提供名字、<c>Query</c> 返回空集），
    /// 其余成员按返回类型给默认值，避免为了一个属性去实现整个 <c>ISQL&lt;T&gt;</c>。
    /// </summary>
    public class SqlInstanceProbe<T> : DispatchProxy
    {
        /// <summary>实例名（<c>SQLParam.Name</c>），同一个管理器里的实例名必须互不相同。</summary>
        public string Name { get; set; } = "probe";

        /// <summary>被查询的次数，用于确认取到的确实是期望的那个实例。</summary>
        public int Queries { get; private set; }

        /// <summary>探针入口；只识别 <c>SQLParam</c> 与 <c>Query</c>，其余按返回类型给默认值。</summary>
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_SQLParam") return new SQLParam { Name = Name };
            if (method.Name == "Query") { Queries++; return new List<T>(); }
            if (method.ReturnType == typeof(void)) return null;
            return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }

    /// <summary>
    /// 手写的 <c>ISQLManager</c> 实现（**不是**具体 <c>SQLManager</c>）：
    /// 只保留 <c>Items</c>/<c>Add</c>/<c>TryGet</c>，其余成员抛 <see cref="NotSupportedException"/>，
    /// 用来证明选择逻辑只依赖 Core 契约。
    /// </summary>
    private sealed class FakeSqlManager : ISQLManager
    {
        private readonly List<ISQL> _items = [];

        public IReadOnlyList<ISQL> Items => _items;

        public int Count => _items.Count;

        public void Config(List<SQLParam> param) => throw new NotSupportedException();

        public ISQL Create(SQLParam param) => throw new NotSupportedException();

        public ISQL<T> Create<T>(SQLParam param, Action<ISQL<T>>? onCreated = null) where T : class, new()
            => throw new NotSupportedException();

        public void Add(ISQL item) => _items.Add(item);

        public bool Remove(ISQL item) => _items.Remove(item);

        public void Clear() => _items.Clear();

        public bool TryGet(string name, out ISQL? item)
        {
            item = _items.FirstOrDefault(candidate =>
                string.Equals(candidate.SQLParam.Name, name, StringComparison.Ordinal));
            return item is not null;
        }
    }
}
