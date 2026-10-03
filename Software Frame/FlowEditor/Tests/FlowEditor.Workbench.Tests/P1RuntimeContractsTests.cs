// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：P1RuntimeContractsTests、ConfigurableNode、CrashValidatorNode、MixedPortNode、CameraLikeNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using WPFCustomControls.PropertyGrid.Core.Models;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

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

    /// <summary>
    /// 验证 Validate_CrashingValidator_BecomesDiagnostic_NotCrash 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、ValidateAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Validate_CrashingValidator_BecomesDiagnostic_NotCrash()
    {
        var (vm, model) = New<CrashValidatorNode>("C");
        model.IsInEnabled = false;
        var report = await FlowNodeValidator.ValidateAsync(
            new List<NodeModel> { model }, new List<ConnectionModel>());
        Assert.Contains(report.Diagnostics, d => d.Code == "E-VALIDATE-CRASH");
    }

    /// <summary>
    /// 验证 Validate_LoopNode_Rejects_ZeroIterations_BeforeRun 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、ValidateAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Validate_LoopNode_Rejects_ZeroIterations_BeforeRun()
    {
        var (vm, model) = New<LoopNode>("循环");
        model.Iterations = 0;
        var report = await FlowNodeValidator.ValidateAsync(
            new List<NodeModel> { model }, new List<ConnectionModel>());
        Assert.Contains(report.Diagnostics, d => d.Code == "E-LOOP-ITERATIONS");
    }

    /// <summary>
    /// 验证 RuntimeInstance_Validate_DoesNotExecute 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Add、ValidateAsync、True、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 RuntimePortReader_DoesNotNeed_PropertyGrid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetDeclaredPorts、Contains、nameof、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RuntimePortReader_DoesNotNeed_PropertyGrid()
    {
        // Runtime 侧只识别新契约：无面板依赖即可做端口审计。
        var ports = NodePortMetadata.GetDeclaredPorts(typeof(MixedPortNode));
        Assert.Contains(ports, p => p.PropertyName == nameof(MixedPortNode.MigratedValue));
        Assert.DoesNotContain(ports, p => p.PropertyName == nameof(MixedPortNode.LegacyValue));
    }

    /// <summary>
    /// 验证 RuntimeState_Members_AreDiscoverable 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetRuntimeStateMembers、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RuntimeState_Members_AreDiscoverable()
    {
        var members = NodePortMetadata.GetRuntimeStateMembers(typeof(LoopNode));
        Assert.Contains(members, m => m.Contains("CurrentIndex") || m.Contains("_completedThisCycle"));
    }

    /// <summary>
    /// 验证 LoopNode_MigratedPorts_Use_NewContract 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、GetDeclaredFlowPorts、First、nameof、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Concurrency_ResourceKey_Declared_ByNode 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Equal、ToList、GetResourceKeys；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Concurrency_ResourceKey_Declared_ByNode()
    {
        var (vm, model) = New<CameraLikeNode>("Cam");
        Assert.Equal(NodeConcurrencyMode.ResourceExclusive, model.ConcurrencyMode);
        Assert.Equal(["Camera:Cam01"], model.GetResourceKeys().ToList());
    }

    /// <summary>
    /// 验证 Affinity_PureCompute_Declares_Any 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Equal；具体条件和先后次序见方法体。
    /// </summary>
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
        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Executed = true;
            return Task.CompletedTask;
        }
        /// <summary>
        /// 验证 ValidateAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public override Task<IEnumerable<FlowDiagnostic>> ValidateAsync(NodeValidationContext ctx) =>
            Task.FromResult<IEnumerable<FlowDiagnostic>>(Problems);
    }

    /// <summary>验证器抛异常的节点。</summary>
    public class CrashValidatorNode : NodeModel
    {
        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
        /// <summary>
        /// 验证 ValidateAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
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

        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>模拟相机节点：同资源串行。</summary>
    public class CameraLikeNode : NodeModel
    {
        public override NodeConcurrencyMode ConcurrencyMode => NodeConcurrencyMode.ResourceExclusive;
        /// <summary>
        /// 验证 GetResourceKeys 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override IEnumerable<string> GetResourceKeys() => ["Camera:Cam01"];
        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
