// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RuntimeIsolationTests、SmokeNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Core.Models、FlowEditor.Core.Nodes、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Core.Models;
using FlowEditor.Core.Nodes;
using Xunit;

namespace FlowEditor.Tests;

/// <summary>验证纯 net9.0 测试进程可独立执行 Runtime 流程而不加载 WPF。</summary>
public sealed class RuntimeIsolationTests
{
    /// <summary>验证纯流程模型和插件元数据由独立的无 WPF Core 程序集拥有。</summary>
    [Fact]
    public void CoreContracts_AreOwnedByStandaloneAssembly()
    {
        var coreAssembly = typeof(PortModel).Assembly;

        Assert.Equal("FlowEditor.Core", coreAssembly.GetName().Name);
        Assert.All(new[]
        {
            typeof(ConnectionModel),
            typeof(FlowChartDocumentModel),
            typeof(NodeStatus),
            typeof(NodeErrorPolicy),
            typeof(FlowToolAttribute),
            typeof(INodeExecutable),
            typeof(NodeTypeIdAttribute),
            typeof(NodeTypeRegistry),
            typeof(NodeConcurrencyMode),
            typeof(FlowLogLevel),
            typeof(FlowLogRoute),
            typeof(IFlowLogSink),
            typeof(FlowDiagnostic),
            typeof(NodeValidationContext),
            typeof(IFlowNodeValidatable),
            typeof(ILoopControlNode),
            typeof(NodeErrorCategory),
            typeof(FlowNodeException),
            typeof(NodeTimeoutException),
            typeof(NodeExecutionContext),
            typeof(INodeContextExecutable),
            typeof(NodeInputAttribute),
            typeof(NodeOutputAttribute),
            typeof(NodeRuntimeStateAttribute),
            typeof(NodePortDeclaration),
            typeof(NodePortMetadata),
            typeof(NodeResourceOwnership),
            typeof(NodeResourceLease<>),
            typeof(NodeResourceScope),
            typeof(ExecutionScopeKind),
            typeof(ExecutionLaneDefinition),
            typeof(IExecutionScopeDescriptor)
        }, contract => Assert.Same(coreAssembly, contract.Assembly));
        Assert.DoesNotContain(coreAssembly.GetReferencedAssemblies(),
            reference => reference.Name is "FlowEditor.Runtime" or "FlowEditor" or
                "PresentationCore" or "PresentationFramework" or "WindowsBase");
        Assert.Contains(typeof(FlowRuntimeInstance<>).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "FlowEditor.Core");
    }

    /// <summary>校验并执行三节点控制流，确认每个节点恰好运行一次且未加载 WPF 程序集。</summary>
    [Fact]
    public async Task ThreeNodeFlow_RunsWithoutWpf()
    {
        var start = new SmokeNode("start", "开始");
        var work = new SmokeNode("work", "任务");
        var end = new SmokeNode("end", "结束");
        var nodes = new[] { start, work, end };
        var connections = new[]
        {
            Connect(start, work),
            Connect(work, end)
        };
        var logs = new List<string>();
        var executableNodes = nodes.Cast<INodeExecutable>().ToList();
        var validation = FlowValidator.Analyze(executableNodes, connections);

        Assert.False(validation.HasErrors, string.Join(Environment.NewLine, validation.Diagnostics));

        var instance = new FlowRuntimeInstance<INodeExecutable>(
            "Runtime smoke", executableNodes, connections, new FlowChartExecutionEngine());
        await instance.RunAsync(CancellationToken.None, logs.Add);

        Assert.All(nodes, node =>
        {
            Assert.Equal(NodeStatus.Success, node.Status);
            Assert.Equal(1, node.Executions);
        });
        Assert.NotEmpty(logs);
        Assert.DoesNotContain(
            AppDomain.CurrentDomain.GetAssemblies(),
            assembly => assembly.GetName().Name is "PresentationCore" or "PresentationFramework" or "WindowsBase");
    }

    /// <summary>创建两个冒烟节点之间的控制流连线。</summary>
    private static ConnectionModel Connect(SmokeNode source, SmokeNode target) => new()
    {
        SourceNodeId = source.Id,
        SourcePortId = source.OutputPorts[0].Id,
        TargetNodeId = target.Id,
        TargetPortId = target.InputPorts[0].Id
    };
}

/// <summary>仅实现 Runtime 最小执行面的无界面测试节点。</summary>
internal sealed class SmokeNode : INodeExecutable
{
    private readonly PortModel _input;
    private readonly PortModel _output;

    /// <summary>创建带标准控制流输入和输出的冒烟节点。</summary>
    public SmokeNode(string id, string name)
    {
        Id = id;
        Name = name;
        _input = PortModel.CreateInput("Next_IN", PortType.Flow, id, PortCategory.Standard);
        _output = PortModel.CreateOutput("Next_Out", PortType.Flow, id, PortCategory.Standard);
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public NodeStatus Status { get; set; }

    /// <inheritdoc />
    public long LastElapsedMs { get; set; }

    /// <inheritdoc />
    public int Count { get; set; }

    /// <inheritdoc />
    public int CurrentRunCount { get; set; }

    /// <inheritdoc />
    public int RunSequence { get; set; }

    /// <inheritdoc />
    public bool IsExcludedFromExecution { get; set; }

    /// <inheritdoc />
    public string? ParentExecutionContainerId { get; set; }

    /// <inheritdoc />
    public string? ExecutionLaneId { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<PortModel> InputPorts => [_input];

    /// <inheritdoc />
    public IReadOnlyList<PortModel> OutputPorts => [_output];

    /// <inheritdoc />
    public IReadOnlyList<string>? ControlFlowPorts => null;

    /// <inheritdoc />
    public FlowJoinMode FlowJoinMode => FlowJoinMode.WaitAllAnyActive;

    /// <summary>记录实际执行次数。</summary>
    public int Executions { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// 验证 FindPortById 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </remarks>
    public PortModel? FindPortById(string portId)
        => _input.Id == portId ? _input : _output.Id == portId ? _output : null;

    /// <inheritdoc />
    /// <remarks>
    /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested；具体条件和先后次序见方法体。
    /// </remarks>
    public Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Executions++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 验证 SyncInputValues 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </remarks>
    public void SyncInputValues()
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// 验证 SyncOutputValues 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </remarks>
    public void SyncOutputValues()
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// 验证 ResetActivation 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </remarks>
    public void ResetActivation()
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// 验证 GetActivatedOutputNames 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </remarks>
    public IReadOnlyCollection<string>? GetActivatedOutputNames() => null;
}
