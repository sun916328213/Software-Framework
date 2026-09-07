using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;

namespace FlowEditorTest;

/// <summary>携带 Int 属性 Value 的直通节点：把输入值 +1 写回输出属性。</summary>
public class ValueNode : NodeModel
{
    private int _value;
    public int Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public ValueNode()
    {
        AddCustomOutputPort("Value", nameof(Value));
        AddCustomInputPort("Value");
    }

    public override Task ExecuteAsync(CancellationToken ct)
    {
        var inPort = FindInputPort("Value");
        if (inPort?.Value is int v)
            Value = v + 1;
        return Task.CompletedTask;
    }
}

/// <summary>恒定输出 100 的源节点。</summary>
public class SourceNode : NodeModel
{
    public int Value { get; set; } = 100;

    public SourceNode() => AddCustomOutputPort("Value", nameof(Value));

    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>会抛异常的故障节点。</summary>
public class FailNode : NodeModel
{
    public override Task ExecuteAsync(CancellationToken ct) => throw new InvalidOperationException("boom");
}

/// <summary>计数节点（用于环路重复调度测试）。</summary>
public class CountingNode : NodeModel
{
    public int Executions;

    public override Task ExecuteAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Executions);
        return Task.CompletedTask;
    }
}

/// <summary>测试辅助：节点/连线构造。</summary>
public static class TestGraph
{
    public static (NodeViewModel Vm, T Model) New<T>(string name) where T : NodeModel, new()
    {
        var model = new T { Name = name };
        return (new NodeViewModel(model), model);
    }

    public static ConnectionModel Flow(NodeViewModel src, NodeViewModel dst)
        => new()
        {
            SourceNodeId = src.Id,
            SourcePortId = $"{src.Id}_Next_Out",
            TargetNodeId = dst.Id,
            TargetPortId = $"{dst.Id}_Next_IN"
        };

    /// <summary>在两个 ValueNode/SourceNode 之间建立 "Value" 属性数据连线。</summary>
    public static ConnectionModel Data(NodeViewModel src, NodeViewModel dst)
        => new()
        {
            SourceNodeId = src.Id,
            SourcePortId = src.Model.OutputPorts.First(p => p.Name == "Value").Id,
            TargetNodeId = dst.Id,
            TargetPortId = dst.Model.InputPorts.First(p => p.Name == "Value").Id
        };
}
