// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ValueNode、SourceNode、FailNode、CountingNode、TestGraph。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Workbench.Tests;

/// <summary>携带 Int 属性 Value 的直通节点：把输入值 +1 写回输出属性。</summary>
public class ValueNode : NodeModel
{
    private int _value;
    public int Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>
    /// 初始化 ValueNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 AddCustomOutputPort、nameof、AddCustomInputPort；具体条件和先后次序见方法体。
    /// </summary>
    public ValueNode()
    {
        AddCustomOutputPort("Value", nameof(Value));
        AddCustomInputPort("Value");
    }

    /// <summary>
    /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FindInputPort；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 初始化 SourceNode 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 AddCustomOutputPort、nameof；具体条件和先后次序见方法体。
    /// </summary>
    public SourceNode() => AddCustomOutputPort("Value", nameof(Value));

    /// <summary>
    /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>会抛异常的故障节点。</summary>
public class FailNode : NodeModel
{
    /// <summary>
    /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public override Task ExecuteAsync(CancellationToken ct) => throw new InvalidOperationException("boom");
}

/// <summary>计数节点（用于环路重复调度测试）。</summary>
public class CountingNode : NodeModel
{
    public int Executions;

    /// <summary>
    /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Increment；具体条件和先后次序见方法体。
    /// </summary>
    public override Task ExecuteAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Executions);
        return Task.CompletedTask;
    }
}

/// <summary>测试辅助：节点/连线构造。</summary>
public static class TestGraph
{
    /// <summary>将以 ViewModel 构造的测试图作为模型图编译。</summary>
    public static CompiledFlowGraph<NodeModel> Compile(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionModel> connections) =>
        CompiledFlowGraph<NodeModel>.Compile(
            nodes.Select(node => node.Model).ToList(), connections);

    /// <summary>
    /// 测试相关入口 New；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public static (NodeViewModel Vm, T Model) New<T>(string name) where T : NodeModel, new()
    {
        var model = new T { Name = name };
        return (new NodeViewModel(model), model);
    }

    /// <summary>
    /// 测试相关入口 Flow；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
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
