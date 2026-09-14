using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;

namespace FlowEditor.Runtime.Smoke;

/// <summary>验证 Runtime 在不加载 WPF 的控制台进程中可独立编译并执行流程。</summary>
internal static class Program
{
    /// <summary>执行三节点控制流，并以进程退出码报告验证结果。</summary>
    public static async Task<int> Main()
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

        if (validation.HasErrors)
        {
            Console.Error.WriteLine($"Runtime smoke failed validation: {validation.Diagnostics[0]}");
            return 1;
        }

        var instance = new FlowRuntimeInstance<INodeExecutable>(
            "Runtime smoke", executableNodes, connections, new FlowChartExecutionEngine());
        await instance.RunAsync(CancellationToken.None, logs.Add);

        var succeeded = nodes.All(node => node.Status == NodeStatus.Success && node.Executions == 1);
        if (!succeeded)
        {
            Console.Error.WriteLine("Runtime smoke failed: the three-node flow did not complete exactly once.");
            return 1;
        }

        Console.WriteLine($"Runtime smoke passed: {nodes.Length} nodes, {connections.Length} connections, {logs.Count} logs.");
        return 0;
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
    public PortModel? FindPortById(string portId)
        => _input.Id == portId ? _input : _output.Id == portId ? _output : null;

    /// <inheritdoc />
    public Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Executions++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void SyncInputValues()
    {
    }

    /// <inheritdoc />
    public void SyncOutputValues()
    {
    }

    /// <inheritdoc />
    public void ResetActivation()
    {
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string>? GetActivatedOutputNames() => null;
}
