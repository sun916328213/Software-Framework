// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SwitchDispatchTests、IntSource、StrSource。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>条件分发节点验收：输入命中唯一分支；空值或未命中时报错；分支与规则可自定义。</summary>
public class SwitchDispatchTests
{
    private readonly TestFlowChartExecutionEngine _engine = new();

    /// <summary>可设值的整数源节点（模拟上游数据）。</summary>
    private sealed class IntSource : NodeModel
    {
        public int Value { get; set; }

        /// <summary>
        /// 初始化 IntSource 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 AddCustomOutputPort、nameof；具体条件和先后次序见方法体。
        /// </summary>
        public IntSource(int value)
        {
            Value = value;
            AddCustomOutputPort("Value", nameof(Value));
        }

        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// StrSource 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class StrSource : NodeModel
    {
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 初始化 StrSource 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 AddCustomOutputPort、nameof；具体条件和先后次序见方法体。
        /// </summary>
        public StrSource(string value)
        {
            Value = value;
            AddCustomOutputPort("Value", nameof(Value));
        }

        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// 验证 New 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static (NodeViewModel Vm, T Model) New<T>(T model) where T : NodeModel
    {
        model.Name ??= typeof(T).Name;
        return (new NodeViewModel(model), model);
    }

    /// <summary>
    /// 验证 Flow 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static ConnectionModel Flow(NodeViewModel src, NodeViewModel dst) => new()
    {
        SourceNodeId = src.Id,
        SourcePortId = $"{src.Id}_Next_Out",
        TargetNodeId = dst.Id,
        TargetPortId = $"{dst.Id}_Next_IN"
    };

    /// <summary>
    /// 验证 Branch 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ConnectionModel Branch(NodeViewModel src, string portName, NodeViewModel dst) => new()
    {
        SourceNodeId = src.Id,
        SourcePortId = src.Model.OutputPorts.First(p => p.Name == portName).Id,
        TargetNodeId = dst.Id,
        TargetPortId = $"{dst.Id}_Next_IN"
    };

    /// <summary>
    /// 验证 Data 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ConnectionModel Data(NodeViewModel src, string srcPort, NodeViewModel dst, string dstPort) => new()
    {
        SourceNodeId = src.Id,
        SourcePortId = src.Model.OutputPorts.First(p => p.Name == srcPort).Id,
        TargetNodeId = dst.Id,
        TargetPortId = dst.Model.InputPorts.First(p => p.Name == dstPort).Id
    };

    /// <summary>
    /// 验证 BuildIntGraph 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Data、Branch；具体条件和先后次序见方法体。
    /// </summary>
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
        var nodes = new List<NodeViewModel> { src, sw, o1, o2 };
        var conns = new List<ConnectionModel>
        {
            Flow(src, sw),
            Data(src, "Value", sw, "输入值"),
            Branch(sw, "OUT1", o1),
            Branch(sw, "OUT2", o2),
        };
        return (nodes, conns);
    }

    /// <summary>
    /// 验证 Executions 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static int Executions(NodeViewModel vm) => ((CountingNode)vm.Model).Executions;

    /// <summary>
    /// 验证 Input1_ActivatesOut1_Only 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildIntGraph、ExecuteAsync、Equal、Executions；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Input2_ActivatesOut2_Only 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildIntGraph、ExecuteAsync、Equal、Executions；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Input2_ActivatesOut2_Only()
    {
        var (nodes, conns) = BuildIntGraph(2, new SwitchNode { Name = "SW" }, out var o1, out var o2, out var d);

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(0, Executions(o1));
        Assert.Equal(1, Executions(o2));
        Assert.Equal(0, Executions(d));
    }

    /// <summary>
    /// 验证 UnmatchedInput_ReportsDispatchError_EvenWhenLegacyDefaultIsEnabled 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildIntGraph、ThrowsAsync、ExecuteAsync、Equal、Executions；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task UnmatchedInput_ReportsDispatchError_EvenWhenLegacyDefaultIsEnabled()
    {
        var (nodes, conns) = BuildIntGraph(9, new SwitchNode { Name = "SW" }, out var o1, out var o2, out var d);

        var error = await Assert.ThrowsAsync<AggregateException>(() =>
            _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }));

        Assert.Equal(0, Executions(o1));
        Assert.Equal(0, Executions(o2));
        Assert.Equal(0, Executions(d));
        Assert.Contains("没有匹配的分发规则", error.ToString());
        Assert.Equal(NodeStatus.Error, nodes[1].Status);
    }

    /// <summary>
    /// 验证 UnmatchedInput_ReportsDispatchError_WhenLegacyDefaultIsDisabled 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildIntGraph、ThrowsAsync、ExecuteAsync、Equal、Executions；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task UnmatchedInput_ReportsDispatchError_WhenLegacyDefaultIsDisabled()
    {
        var sw = new SwitchNode { Name = "SW", UseDefaultBranch = false };
        var (nodes, conns) = BuildIntGraph(9, sw, out var o1, out var o2, out var d);

        var error = await Assert.ThrowsAsync<AggregateException>(() =>
            _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }));

        Assert.Equal(0, Executions(o1));
        Assert.Equal(0, Executions(o2));
        Assert.Equal(0, Executions(d));
        Assert.Contains("没有匹配的分发规则", error.ToString());
        Assert.Equal(NodeStatus.Error, nodes[1].Status);
    }

    /// <summary>
    /// 验证 EmptyInput_ReportsDispatchError_AndStopsEveryDownstreamBranch 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Branch、Flow、ThrowsAsync、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyInput_ReportsDispatchError_AndStopsEveryDownstreamBranch(string? input)
    {
        var sw = new SwitchNode
        {
            Name = "SW",
            InputValue = input,
            RulesText = "1=OUT1;2=OUT2",
            UseDefaultBranch = true
        };
        var swVm = new NodeViewModel(sw);
        var out1 = New(new CountingNode { Name = "OUT1" }).Vm;
        var after1 = New(new CountingNode { Name = "AFTER1" }).Vm;
        var nodes = new List<NodeViewModel> { swVm, out1, after1 };
        var conns = new List<ConnectionModel>
        {
            Branch(swVm, "OUT1", out1),
            Flow(out1, after1)
        };

        var error = await Assert.ThrowsAsync<AggregateException>(() =>
            _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }));

        Assert.Contains("输入值为空", error.ToString());
        Assert.Equal(NodeStatus.Error, swVm.Status);
        Assert.All([out1, after1], node =>
        {
            Assert.Equal(0, Executions(node));
            Assert.Equal(NodeStatus.Idle, node.Status);
        });
    }

    /// <summary>
    /// 验证 NodeCanvas_OnlyProvidesRuleEditorAction 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Single、Equal、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NodeCanvas_OnlyProvidesRuleEditorAction()
    {
        var sw = new SwitchNode();

        var action = Assert.Single(sw.ActionDescriptors);
        Assert.Equal("编辑分发规则", action.Name);
        Assert.DoesNotContain(sw.OutputPorts, port => port.Name == SwitchNode.EmptyBranchName);
        Assert.DoesNotContain(sw.OutputPorts, port => port.Name == SwitchNode.DefaultBranchName);
    }

    /// <summary>
    /// 验证 CustomBranchName_AutoCreatesPort_AndActivates 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Data、ExecuteAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 StringMatch_IsCaseInsensitive_ByDefault 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Data、Branch、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 BranchCount_SyncsNumberedPorts 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Contains、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
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
        Assert.DoesNotContain(sw.ControlFlowPorts!, n => n == SwitchNode.DefaultBranchName);

        sw.BranchCount = 2;
        Assert.DoesNotContain(sw.CustomOutputPorts, p => p.Name == "OUT3");
        Assert.DoesNotContain(sw.CustomOutputPorts, p => p.Name == "OUT4");
    }

    /// <summary>
    /// 验证 ParseRules_SupportsComments_AndColonSeparator 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ParseRules、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ParseRules_SupportsComments_AndColonSeparator()
    {
        var rules = SwitchNode.ParseRules("# 注释\n1:OUT1\n// 注释\n2=OUT2\n\n无效行\n");
        Assert.Equal([( "1", "OUT1" ), ( "2", "OUT2" )], rules);
    }

    /// <summary>
    /// 验证 CustomRules_SupportSemicolons_AndSynchronizeNamedFlowPortsImmediately 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、ParseRules、All、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CustomRules_SupportSemicolons_AndSynchronizeNamedFlowPortsImmediately()
    {
        var sw = new SwitchNode
        {
            RulesText = "A=通过;B=返工；C:报废"
        };

        Assert.Equal(
            [("A", "通过"), ("B", "返工"), ("C", "报废")],
            SwitchNode.ParseRules(sw.RulesText));
        Assert.All(["通过", "返工", "报废"], name =>
            Assert.Contains(sw.OutputPorts, port => port.Name == name && port.DataType == PortType.Flow));
    }

    /// <summary>
    /// 验证 InputToString_UnpacksJsonElement_AfterFlowReload 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、Equal、InputToString；具体条件和先后次序见方法体。
    /// </summary>
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
