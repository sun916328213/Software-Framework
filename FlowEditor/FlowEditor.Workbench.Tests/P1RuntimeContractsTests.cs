using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using WPFCustomControls.PropertyGrid.Models;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>P1 验收：Validate 阶段、配置/端口/状态分离、并发/亲和性、元数据解耦。</summary>
public class P1RuntimeContractsTests
{
    // ── Validate 阶段 ──

    [Fact]
    public async Task Validate_FindsConfigProblem_WithoutExecuting()
    {
        var (vm, model) = New<ConfigurableNode>("C");
        model.Problems.Add(new FlowDiagnostic(FlowDiagnosticSeverity.Error, "E-DEMO-CONFIG", "演示配置错误"));
        model.IsInEnabled = false;
        var report = await FlowNodeValidator.ValidateAsync(
            new List<NodeModel> { model }, new List<ConnectionModel>());
        Assert.Contains(report.Diagnostics, d => d.Code == "E-DEMO-CONFIG");
        Assert.True(report.HasErrors);
        Assert.False(model.Executed);
    }

    [Fact]
    public async Task Validate_CrashingValidator_BecomesDiagnostic_NotCrash()
    {
        var (vm, model) = New<CrashValidatorNode>("C");
        model.IsInEnabled = false;
        var report = await FlowNodeValidator.ValidateAsync(
            new List<NodeModel> { model }, new List<ConnectionModel>());
        Assert.Contains(report.Diagnostics, d => d.Code == "E-VALIDATE-CRASH");
    }

    [Fact]
    public async Task Validate_LoopNode_Rejects_ZeroIterations_BeforeRun()
    {
        var (vm, model) = New<LoopNode>("循环");
        model.Iterations = 0;
        var report = await FlowNodeValidator.ValidateAsync(
            new List<NodeModel> { model }, new List<ConnectionModel>());
        Assert.Contains(report.Diagnostics, d => d.Code == "E-LOOP-ITERATIONS");
    }

    [Fact]
    public async Task RuntimeInstance_Validate_DoesNotExecute()
    {
        var (vm, model) = New<ConfigurableNode>("C");
        model.IsInEnabled = false;
        model.Problems.Add(new FlowDiagnostic(FlowDiagnosticSeverity.Error, "E-DEMO-CONFIG", "演示配置错误"));
        var instance = new FlowRuntimeInstance<NodeModel>(
            "validate-only", new List<NodeModel> { model }, new List<ConnectionModel>(),
            new FlowChartExecutionEngine());
        var report = await instance.ValidateAsync();
        Assert.True(report.HasErrors);
        Assert.False(model.Executed);
        Assert.Equal(NodeStatus.Idle, model.Status);
    }

    // ── 配置/输入/输出/RuntimeState 分离 + 元数据解耦 ──

    [Fact]
    public void DeclaredPorts_Prefer_NewContract_Over_Legacy()
    {
        var (vm, model) = New<MixedPortNode>("M");
        var ports = model.GetDeclaredFlowPorts();
        var migrated = ports.First(p => p.PropertyName == nameof(MixedPortNode.MigratedValue));
        Assert.False(migrated.FromLegacy);
        Assert.True(migrated.IsInput);
        var legacy = ports.First(p => p.PropertyName == nameof(MixedPortNode.LegacyValue));
        Assert.True(legacy.FromLegacy);
        Assert.False(legacy.IsInput);
    }

    [Fact]
    public void RuntimePortReader_DoesNotNeed_PropertyGrid()
    {
        // Runtime 侧只识别新契约：无面板依赖即可做端口审计。
        var ports = NodePortMetadata.GetDeclaredPorts(typeof(MixedPortNode));
        Assert.Contains(ports, p => p.PropertyName == nameof(MixedPortNode.MigratedValue));
        Assert.DoesNotContain(ports, p => p.PropertyName == nameof(MixedPortNode.LegacyValue));
    }

    [Fact]
    public void RuntimeState_Members_AreDiscoverable()
    {
        var members = NodePortMetadata.GetRuntimeStateMembers(typeof(LoopNode));
        Assert.Contains(members, m => m.Contains("CurrentIndex") || m.Contains("_completedThisCycle"));
    }

    [Fact]
    public void LoopNode_MigratedPorts_Use_NewContract()
    {
        var (vm, model) = New<LoopNode>("循环");
        var ports = model.GetDeclaredFlowPorts();
        var iterations = ports.First(p => p.PropertyName == nameof(LoopNode.Iterations));
        Assert.False(iterations.FromLegacy);
        Assert.True(iterations.IsInput);
    }

    // ── 并发/亲和性 ──

    [Fact]
    public void Concurrency_Defaults_Preserve_CurrentBehavior()
    {
        var (vm, model) = New<ConfigurableNode>("C");
        Assert.Equal(NodeConcurrencyMode.Reentrant, model.ConcurrencyMode);
        Assert.Empty(model.GetResourceKeys());
        Assert.Equal(NodeExecutionAffinity.Background, model.Affinity);
    }

    [Fact]
    public void Concurrency_ResourceKey_Declared_ByNode()
    {
        var (vm, model) = New<CameraLikeNode>("Cam");
        Assert.Equal(NodeConcurrencyMode.ResourceExclusive, model.ConcurrencyMode);
        Assert.Equal(["Camera:Cam01"], model.GetResourceKeys().ToList());
    }

    [Fact]
    public void Affinity_PureCompute_Declares_Any()
    {
        var (vm, model) = New<LoopNode>("循环");
        Assert.Equal(NodeExecutionAffinity.Any, model.Affinity);
    }

    // ── 测试 doubles ──

    /// <summary>可配置诊断 + 执行标记的通用测试节点。</summary>
    public class ConfigurableNode : NodeModel
    {
        public readonly List<FlowDiagnostic> Problems = [];
        public bool Executed;
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Executed = true;
            return Task.CompletedTask;
        }
        public override Task<IEnumerable<FlowDiagnostic>> ValidateAsync(NodeValidationContext ctx) =>
            Task.FromResult<IEnumerable<FlowDiagnostic>>(Problems);
    }

    /// <summary>验证器抛异常的节点。</summary>
    public class CrashValidatorNode : NodeModel
    {
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
        public override Task<IEnumerable<FlowDiagnostic>> ValidateAsync(NodeValidationContext ctx) =>
            throw new InvalidOperationException("validator boom");
    }

    /// <summary>同时携带新旧端口声明的节点：迁移 vs 未迁移。</summary>
    public class MixedPortNode : NodeModel
    {
        [NodeProperty(DisplayName = "已迁移", IsInput = true)]
        [NodeInput(Description = "已迁到新契约的输入")]
        public int MigratedValue { get; set; }

        [NodeProperty(DisplayName = "未迁移", IsOutput = true)]
        public int LegacyValue { get; set; }

        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>模拟相机节点：同资源串行。</summary>
    public class CameraLikeNode : NodeModel
    {
        public override NodeConcurrencyMode ConcurrencyMode => NodeConcurrencyMode.ResourceExclusive;
        public override IEnumerable<string> GetResourceKeys() => ["Camera:Cam01"];
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
