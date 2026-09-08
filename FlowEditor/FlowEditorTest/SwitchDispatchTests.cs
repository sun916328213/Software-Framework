using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;
using Xunit;

namespace FlowEditorTest;

/// <summary>条件分发节点验收：输入 1 走 OUT1、输入 2 走 OUT2，未命中走 Default，分支与规则可自定义。</summary>
public class SwitchDispatchTests
{
    private readonly FlowChartExecutionEngine _engine = new();

    /// <summary>可设值的整数源节点（模拟上游数据）。</summary>
    private sealed class IntSource : NodeModel
    {
        public int Value { get; set; }

        public IntSource(int value)
        {
            Value = value;
            AddCustomOutputPort("Value", nameof(Value));
        }

        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StrSource : NodeModel
    {
        public string Value { get; set; } = string.Empty;

        public StrSource(string value)
        {
            Value = value;
            AddCustomOutputPort("Value", nameof(Value));
        }

        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static (NodeViewModel Vm, T Model) New<T>(T model) where T : NodeModel
    {
        model.Name ??= typeof(T).Name;
        return (new NodeViewModel(model), model);
    }

    private static ConnectionModel Flow(NodeViewModel src, NodeViewModel dst) => new()
    {
        SourceNodeId = src.Id,
        SourcePortId = $"{src.Id}_Next_Out",
        TargetNodeId = dst.Id,
        TargetPortId = $"{dst.Id}_Next_IN"
    };

    private static ConnectionModel Branch(NodeViewModel src, string portName, NodeViewModel dst) => new()
    {
        SourceNodeId = src.Id,
        SourcePortId = src.Model.OutputPorts.First(p => p.Name == portName).Id,
        TargetNodeId = dst.Id,
        TargetPortId = $"{dst.Id}_Next_IN"
    };

    private static ConnectionModel Data(NodeViewModel src, string srcPort, NodeViewModel dst, string dstPort) => new()
    {
        SourceNodeId = src.Id,
        SourcePortId = src.Model.OutputPorts.First(p => p.Name == srcPort).Id,
        TargetNodeId = dst.Id,
        TargetPortId = dst.Model.InputPorts.First(p => p.Name == dstPort).Id
    };

    private static (List<NodeViewModel> Nodes, List<ConnectionModel> Conns) BuildIntGraph(
        int input, SwitchNode switchModel,
        out NodeViewModel out1, out NodeViewModel out2, out NodeViewModel def)
    {
        var (src, _) = New(new IntSource(input) { Name = "SRC" });
        var sw = new NodeViewModel(switchModel);
        var (o1, _) = New(new CountingNode { Name = "OUT1-T" });
        var (o2, _) = New(new CountingNode { Name = "OUT2-T" });
        var (d, _) = New(new CountingNode { Name = "DEF-T" });
        out1 = o1; out2 = o2; def = d;
        var nodes = new List<NodeViewModel> { src, sw, o1, o2, d };
        var conns = new List<ConnectionModel>
        {
            Flow(src, sw),
            Data(src, "Value", sw, "输入值"),
            Branch(sw, "OUT1", o1),
            Branch(sw, "OUT2", o2),
            Branch(sw, "Default", d),
        };
        return (nodes, conns);
    }

    private static int Executions(NodeViewModel vm) => ((CountingNode)vm.Model).Executions;

    [Fact]
    public async Task Input1_ActivatesOut1_Only()
    {
        var (nodes, conns) = BuildIntGraph(1, new SwitchNode { Name = "SW" }, out var o1, out var o2, out var d);

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(1, Executions(o1));
        Assert.Equal(0, Executions(o2));
        Assert.Equal(0, Executions(d));
        Assert.Equal(NodeStatus.Success, o1.Status);
        Assert.Equal(NodeStatus.Idle, o2.Status);
        Assert.Equal(NodeStatus.Idle, d.Status);
    }

    [Fact]
    public async Task Input2_ActivatesOut2_Only()
    {
        var (nodes, conns) = BuildIntGraph(2, new SwitchNode { Name = "SW" }, out var o1, out var o2, out var d);

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(0, Executions(o1));
        Assert.Equal(1, Executions(o2));
        Assert.Equal(0, Executions(d));
    }

    [Fact]
    public async Task UnmatchedInput_GoesToDefault()
    {
        var (nodes, conns) = BuildIntGraph(9, new SwitchNode { Name = "SW" }, out var o1, out var o2, out var d);

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(0, Executions(o1));
        Assert.Equal(0, Executions(o2));
        Assert.Equal(1, Executions(d));
        Assert.Equal("Default", ((SwitchNode)nodes[1].Model).LastMatchedBranch);
    }

    [Fact]
    public async Task UnmatchedInput_WithoutDefault_SkipsAll()
    {
        var sw = new SwitchNode { Name = "SW", UseDefaultBranch = false };
        var (nodes, conns) = BuildIntGraph(9, sw, out var o1, out var o2, out var d);

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(0, Executions(o1));
        Assert.Equal(0, Executions(o2));
        Assert.Equal(0, Executions(d));
        Assert.Equal(NodeStatus.Success, nodes[1].Status); // 分发节点自身仍算成功
    }

    [Fact]
    public async Task CustomBranchName_AutoCreatesPort_AndActivates()
    {
        var sw = new SwitchNode { Name = "SW", RulesText = "1=合格\n2=不合格" };
        var (src, _) = New(new IntSource(2) { Name = "SRC" });
        var swVm = new NodeViewModel(sw);

        // 第一轮：图中只有源与分发（无分支下游），执行时自动补齐规则分支端口
        var nodes = new List<NodeViewModel> { src, swVm };
        var conns = new List<ConnectionModel> { Flow(src, swVm), Data(src, "Value", swVm, "输入值") };
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // 规则右侧分支端口应已自动创建
        Assert.Contains(sw.CustomOutputPorts, p => p.Name == "合格");
        Assert.Contains(sw.CustomOutputPorts, p => p.Name == "不合格");
        Assert.Equal("不合格", sw.LastMatchedBranch);

        // 第二轮：补上分支连线与下游，命中分支执行、未命中分支跳过
        var ok = New(new CountingNode { Name = "OK-T" }).Vm;
        var ng = New(new CountingNode { Name = "NG-T" }).Vm;
        nodes.AddRange([ok, ng]);
        conns.Add(Branch(swVm, "合格", ok));
        conns.Add(Branch(swVm, "不合格", ng));
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });
        Assert.Equal(0, Executions(ok));
        Assert.Equal(1, Executions(ng));
    }

    [Fact]
    public async Task StringMatch_IsCaseInsensitive_ByDefault()
    {
        var sw = new SwitchNode { Name = "SW", RulesText = "auto=OUT1\nmanual=OUT2" };
        var (src, _) = New(new StrSource("AUTO") { Name = "SRC" });
        var swVm = new NodeViewModel(sw);
        var o1 = New(new CountingNode { Name = "O1" }).Vm;
        var o2 = New(new CountingNode { Name = "O2" }).Vm;
        var nodes = new List<NodeViewModel> { src, swVm, o1, o2 };
        var conns = new List<ConnectionModel>
        {
            Flow(src, swVm),
            Data(src, "Value", swVm, "输入值"),
            Branch(swVm, "OUT1", o1),
            Branch(swVm, "OUT2", o2),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(1, Executions(o1));
        Assert.Equal(0, Executions(o2));
    }

    [Fact]
    public void BranchCount_SyncsNumberedPorts()
    {
        var sw = new SwitchNode { Name = "SW" };
        Assert.Contains(sw.CustomOutputPorts, p => p.Name == "OUT1");
        Assert.Contains(sw.CustomOutputPorts, p => p.Name == "OUT2");

        sw.BranchCount = 4;
        Assert.Contains(sw.CustomOutputPorts, p => p.Name == "OUT3");
        Assert.Contains(sw.CustomOutputPorts, p => p.Name == "OUT4");
        Assert.Contains(sw.ControlFlowPorts!, n => n == "OUT4");
        Assert.Contains(sw.ControlFlowPorts!, n => n == "Default");
    }

    [Fact]
    public void ParseRules_SupportsComments_AndColonSeparator()
    {
        var rules = SwitchNode.ParseRules("# 注释\n1:OUT1\n// 注释\n2=OUT2\n\n无效行\n");
        Assert.Equal([( "1", "OUT1" ), ( "2", "OUT2" )], rules);
    }

    [Fact]
    public void InputToString_UnpacksJsonElement_AfterFlowReload()
    {
        // .flow 反序列化后字符串属性会暂存为 JsonElement，直接 ToString 会带引号
        using var strDoc = System.Text.Json.JsonDocument.Parse("\"AUTO\"");
        using var numDoc = System.Text.Json.JsonDocument.Parse("1");
        Assert.Equal("AUTO", SwitchNode.InputToString(strDoc.RootElement));
        Assert.Equal("1", SwitchNode.InputToString(numDoc.RootElement));
        Assert.Equal("2", SwitchNode.InputToString(2));
        Assert.Equal(string.Empty, SwitchNode.InputToString(null));
    }
}
