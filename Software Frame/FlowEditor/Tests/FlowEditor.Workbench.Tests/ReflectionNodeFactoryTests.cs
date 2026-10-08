// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ReflectionNodeFactoryTests、DiService、DiOnlyNode、GreedyNode、UnresolvedNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using Xunit;
using System.IO;

namespace FlowEditor.Workbench.Tests;

/// <summary>P3 补充：ReflectionNodeFactory 的 DI 构造函数解析能力。</summary>
public class ReflectionNodeFactoryTests
{
    private readonly ReflectionNodeFactory _factory = new();

    // ── 测试用 DI 节点 ──

    public class DiService
    {
        public string Tag { get; } = "svc";
    }

    /// <summary>仅 DI 构造（无无参构造）。</summary>
    public class DiOnlyNode : NodeModel
    {
        public DiService Injected { get; }
        /// <summary>
        /// 初始化 DiOnlyNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DiOnlyNode(DiService service) => Injected = service;
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>双构造：贪心应选择参数更多且可满足的。</summary>
    public class GreedyNode : NodeModel
    {
        public string Source { get; }
        /// <summary>
        /// 初始化 GreedyNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public GreedyNode()
        {
            Source = "parameterless";
        }
        /// <summary>
        /// 初始化 GreedyNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public GreedyNode(DiService s1, DiService s2)
        {
            Source = $"di:{s1.Tag}/{s2.Tag}";
        }
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>依赖未注册服务的节点。</summary>
    public class UnresolvedNode : NodeModel
    {
        /// <summary>
        /// 初始化 UnresolvedNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public UnresolvedNode(Stream stream) { }
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// 测试相关入口 Desc；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static NodeDocumentModel Desc<T>() where T : NodeModel
        => new() { Id = "t", TypeName = typeof(T).AssemblyQualifiedName!, Name = "n" };

    /// <summary>按节点类型创建时不需要先构造文档描述符。</summary>
    [Fact]
    public void CreateByType_ReturnsIndependentNode()
    {
        var first = _factory.Create(typeof(GreedyNode));
        var second = _factory.Create(typeof(GreedyNode));

        Assert.IsType<GreedyNode>(first);
        Assert.IsType<GreedyNode>(second);
        Assert.NotSame(first, second);
    }

    /// <summary>一个 factory 的依赖注册不会泄漏到另一个 factory。</summary>
    [Fact]
    public void RegisteredServices_AreIsolatedPerFactoryInstance()
    {
        var configured = new ReflectionNodeFactory();
        var isolated = new ReflectionNodeFactory();
        configured.RegisterService(new DiService());

        Assert.IsType<DiOnlyNode>(configured.Create(typeof(DiOnlyNode)));
        Assert.Throws<InvalidOperationException>(() => isolated.Create(typeof(DiOnlyNode)));
    }

    /// <summary>
    /// 测试相关入口 DiCtor_ResolvedViaRegisterService；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterService、Create、Desc、IsType、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DiCtor_ResolvedViaRegisterService()
    {
        _factory.RegisterService(new DiService());

        var node = _factory.Create(Desc<DiOnlyNode>());

        var di = Assert.IsType<DiOnlyNode>(node);
        Assert.Equal("svc", di.Injected.Tag);
    }

    /// <summary>
    /// 测试相关入口 DiCtor_ResolvedViaInstanceFallback；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、Desc、NotNull、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DiCtor_ResolvedViaInstanceFallback()
    {
        var factory = new ReflectionNodeFactory(
            type => type == typeof(DiService) ? new DiService() : null);

        var node = factory.Create(Desc<DiOnlyNode>());

        Assert.NotNull(Assert.IsType<DiOnlyNode>(node).Injected);
    }

    /// <summary>
    /// 测试相关入口 GreedyCtor_PrefersResolvableParameterful_OverParameterless；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterService、Create、Desc、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void GreedyCtor_PrefersResolvableParameterful_OverParameterless()
    {
        _factory.RegisterService(new DiService());

        var node = (GreedyNode)_factory.Create(Desc<GreedyNode>());

        // 无参构造存在，但两参构造可全部满足 → 贪心选择后者
        Assert.StartsWith("di:", node.Source);
    }

    /// <summary>
    /// 测试相关入口 Parameterless_WinsWhenNoServicesRegistered；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、Desc、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Parameterless_WinsWhenNoServicesRegistered()
    {
        var node = (GreedyNode)_factory.Create(Desc<GreedyNode>());

        Assert.Equal("parameterless", node.Source);
    }

    /// <summary>
    /// 测试相关入口 MissingDependency_ThrowsWithServiceNames；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Create、Desc、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MissingDependency_ThrowsWithServiceNames()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _factory.Create(Desc<UnresolvedNode>()));

        Assert.Contains("Stream", ex.Message);       // 报出缺失的服务类型
        Assert.Contains("RegisterService", ex.Message); // 给出修复指引
    }
}
