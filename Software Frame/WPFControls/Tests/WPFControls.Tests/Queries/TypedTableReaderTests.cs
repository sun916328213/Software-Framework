// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TypedTableReaderTests、ProbeRow、SqlRowProbe。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.SQL、WPFControls.Integration.Queries、WPFControls.ProgramControl.Models、System.Reflection、Xunit。导入不代表所有类型都会在运行时加载。
using DataManager.Core.SQL;
using WPFControls.Integration.Queries;
using WPFControls.ProgramControl.Models;
using System.Reflection;
using Xunit;

namespace WPFControls.Tests.Queries;

/// <summary>
/// T11 专项：<c>SqlTableQueryReader&lt;T&gt;</c> 自身的取实例契约 —— 惰性、每次现取、取不到时明确失败。
/// </summary>
/// <remarks>
/// <para>宿主侧的按类型登记（<c>SqlInstanceComposition</c>）与端到端查询路径由
/// <c>MachineApp.Host.Tests</c> 覆盖；这里只钉 reader 自己的契约，
/// 因为它是 <c>internal</c>，只有本工程能直接 <c>new</c> 出来。</para>
/// <para><b>为什么非要有这个文件</b>：reader 里「宿主忘了登记」的兜底分支，经容器其实**到不了** ——
/// DryIoc 对未登记服务会自己抛 <c>UnableToResolveUnknownService</c>，不会把 null 交出来。
/// 若不在这里直接构造 reader，那条分支就是**没有任何测试的代码**，
/// 在变异测试里表现为「删掉也不红」的存活变异体。</para>
/// </remarks>
public sealed class TypedTableReaderTests
{
    /// <summary>测试用的固定日期，避免依赖当天时间。</summary>
    private static readonly DateTime Day = new(2026, 10, 1);

    /// <summary>
    /// 惰性 + 每次现取：构造期**不**取实例；每次 <c>Read</c> 都重新调用获取函数 ——
    /// 于是「档案切换后重建实例」能被读者立刻看到，而不是永远抱着构造期那一个。
    /// </summary>
    /// <remarks>
    /// 变异点：把实例在构造期缓存成字段（<c>_sql = getSql()</c>），本用例红。
    /// </remarks>
    [Fact]
    public void ReaderTakesInstanceFromGetterOnEveryRead()
    {
        var first = DispatchProxy.Create<ISQL<ProbeRow>, SqlRowProbe<ProbeRow>>();
        var second = DispatchProxy.Create<ISQL<ProbeRow>, SqlRowProbe<ProbeRow>>();
        var firstProbe = (SqlRowProbe<ProbeRow>)(object)first;
        var secondProbe = (SqlRowProbe<ProbeRow>)(object)second;

        ISQL<ProbeRow> current = first;
        var calls = 0;
        var reader = new SqlTableQueryReader<ProbeRow>(() => { calls++; return current; });

        Assert.Equal(0, calls);
        reader.Read(Day, Day, null);
        Assert.Equal(1, calls);
        Assert.Equal(1, firstProbe.Reads);
        Assert.Equal(0, secondProbe.Reads);

        current = second;
        reader.Read(Day, Day, "id");
        Assert.Equal(2, calls);
        Assert.Equal(1, firstProbe.Reads);
        Assert.Equal(1, secondProbe.Reads);
    }

    /// <summary>
    /// 取不到实例时抛 <see cref="InvalidOperationException"/>（**不是** <see cref="NullReferenceException"/>），
    /// 且消息里带实体全名 —— 即「缺配置明确失败」，与改造前 <c>manager[0]</c> 的
    /// <c>InvalidCastException</c> / <c>IndexOutOfRangeException</c> 一样响，但可读。
    /// </summary>
    /// <remarks>
    /// 变异点：删掉 <c>?? throw ...</c> 兜底，本用例红（退化成 NullReferenceException）。
    /// </remarks>
    [Fact]
    public void ReaderFailsLoudlyWhenGetterYieldsNoInstance()
    {
        var reader = new SqlTableQueryReader<ProbeRow>(() => null!);

        var error = Assert.Throws<InvalidOperationException>(() => reader.Read(Day, Day, null));
        Assert.Contains(typeof(ProbeRow).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains("宿主必须为该查询实体注册", error.Message, StringComparison.Ordinal);
    }

    /// <summary>类型化查询行：满足 <see cref="ISQLDataBase"/> 约束（<c>ID</c> + <c>Time</c>）。</summary>
    public sealed class ProbeRow : ISQLDataBase
    {
        /// <inheritdoc />
        public string ID { get; set; } = string.Empty;

        /// <inheritdoc />
        public DateTime Time { get; set; }
    }

    /// <summary>
    /// SQL 探针：只实现取实例逻辑真正走到的 <c>Query</c>，其余成员按返回类型给默认值，
    /// 避免为了一个入口去实现整个 <c>ISQL&lt;T&gt;</c>。与 <c>QueryArchitectureHostTests.SqlProbe&lt;T&gt;</c> 同一手法。
    /// </summary>
    public class SqlRowProbe<T> : DispatchProxy
    {
        /// <summary>探针被查询的次数，用来确认 reader 每次都向获取函数要「当下」的实例。</summary>
        public int Reads { get; private set; }

        /// <summary>
        /// 探针入口；只识别 <c>Query</c>（记一次读取并返回空集，本类不校验行内容），
        /// 其余按返回类型给默认值。
        /// </summary>
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "Query")
            {
                Reads++;
                return new List<T>();
            }
            if (method.ReturnType == typeof(void)) return null;
            return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
}
