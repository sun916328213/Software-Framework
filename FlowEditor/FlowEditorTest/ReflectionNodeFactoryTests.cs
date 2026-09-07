using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using Xunit;
using System.IO;

namespace FlowEditorTest;

/// <summary>P3 补充：ReflectionNodeFactory 的 DI 构造函数解析能力。</summary>
public class ReflectionNodeFactoryTests : IDisposable
{
    public ReflectionNodeFactoryTests()
    {
        _previousFallback = ReflectionNodeFactory.FallbackResolver;
    }

    private readonly Func<Type, object?>? _previousFallback;

    public void Dispose()
    {
        ReflectionNodeFactory.ClearServices();
        ReflectionNodeFactory.FallbackResolver = _previousFallback;
        GC.SuppressFinalize(this);
    }

    // ── 测试用 DI 节点 ──

    public class DiService
    {
        public string Tag { get; } = "svc";
    }

    /// <summary>仅 DI 构造（无无参构造）。</summary>
    public class DiOnlyNode : NodeModel
    {
        public DiService Injected { get; }
        public DiOnlyNode(DiService service) => Injected = service;
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>双构造：贪心应选择参数更多且可满足的。</summary>
    public class GreedyNode : NodeModel
    {
        public string Source { get; }
        public GreedyNode()
        {
            Source = "parameterless";
        }
        public GreedyNode(DiService s1, DiService s2)
        {
            Source = $"di:{s1.Tag}/{s2.Tag}";
        }
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>依赖未注册服务的节点。</summary>
    public class UnresolvedNode : NodeModel
    {
        public UnresolvedNode(Stream stream) { }
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static NodeDocumentModel Desc<T>() where T : NodeModel
        => new() { Id = "t", TypeName = typeof(T).AssemblyQualifiedName!, Name = "n" };

    [Fact]
    public void DiCtor_ResolvedViaRegisterService()
    {
        ReflectionNodeFactory.RegisterService(new DiService());

        var node = ReflectionNodeFactory.Instance.Create(Desc<DiOnlyNode>());

        var di = Assert.IsType<DiOnlyNode>(node);
        Assert.Equal("svc", di.Injected.Tag);
    }

    [Fact]
    public void DiCtor_ResolvedViaFallbackResolver()
    {
        ReflectionNodeFactory.FallbackResolver = t => t == typeof(DiService) ? new DiService() : null;

        var node = ReflectionNodeFactory.Instance.Create(Desc<DiOnlyNode>());

        Assert.NotNull(Assert.IsType<DiOnlyNode>(node).Injected);
    }

    [Fact]
    public void GreedyCtor_PrefersResolvableParameterful_OverParameterless()
    {
        ReflectionNodeFactory.RegisterService(new DiService());

        var node = (GreedyNode)ReflectionNodeFactory.Instance.Create(Desc<GreedyNode>());

        // 无参构造存在，但两参构造可全部满足 → 贪心选择后者
        Assert.StartsWith("di:", node.Source);
    }

    [Fact]
    public void Parameterless_WinsWhenNoServicesRegistered()
    {
        var node = (GreedyNode)ReflectionNodeFactory.Instance.Create(Desc<GreedyNode>());

        Assert.Equal("parameterless", node.Source);
    }

    [Fact]
    public void MissingDependency_ThrowsWithServiceNames()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ReflectionNodeFactory.Instance.Create(Desc<UnresolvedNode>()));

        Assert.Contains("Stream", ex.Message);       // 报出缺失的服务类型
        Assert.Contains("RegisterService", ex.Message); // 给出修复指引
    }
}
