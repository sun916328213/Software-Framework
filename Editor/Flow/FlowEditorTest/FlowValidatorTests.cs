using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>P2 验收：运行前静态分析一次性报告，类型错误不再等到执行中才崩。</summary>
public class FlowValidatorTests
{
    [Fact]
    public void MissingInConnection_ProducesError()
    {
        var orphanEntry = New<CountingNode>("A").Vm; // 无任何入线
        var nodes = new List<NodeViewModel> { orphanEntry };
        var conns = new List<ConnectionModel>();

        var report = FlowValidator.Analyze(nodes, conns);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Diagnostics, d => d.Code == "E-IN-REQ");
    }

    [Fact]
    public void TypeMismatch_DetectedBeforeRun()
    {
        var aModel = new SourceNode { Name = "A" };
        aModel.OutputPorts.First(p => p.Name == "Value").DataType = PortType.String; // 输出声明为 String
        var a = new NodeViewModel(aModel);
        var b = New<ValueNode>("B").Vm;
        b.Model.InputPorts.First(p => p.Name == "Value").DataType = PortType.Integer; // 输入声明为 Integer

        var dataConn = Data(a, b);
        var nodes = new List<NodeViewModel> { a, b };
        var conns = new List<ConnectionModel> { Flow(a, b), dataConn };

        var report = FlowValidator.Analyze(nodes, conns);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Diagnostics, d => d.Code == "E-TYPE");
    }

    [Fact]
    public void UnreachableNode_ProducesWarning_NotError()
    {
        // 干净主链：入口桩(不勾选IN) → A → B
        var entryModel = new CountingNode { Name = "ENTRY", IsInEnabled = false };
        var entry = new NodeViewModel(entryModel);
        var (a, _) = New<ValueNode>("A");
        var (b, _) = New<ValueNode>("B");
        // 孤岛：无任何连线且不勾选 IN
        var island = new NodeViewModel(new CountingNode { Name = "ISLAND", IsInEnabled = false });
        var nodes = new List<NodeViewModel> { entry, a, b, island };
        var conns = new List<ConnectionModel> { Flow(entry, a), Flow(a, b) };

        var report = FlowValidator.Analyze(nodes, conns);

        Assert.False(report.HasErrors);                     // 孤岛仅警告，不阻止执行
        Assert.Contains(report.Diagnostics,
            d => d.Code == "W-ORPHAN" && d.Message.Contains("ISLAND"));
    }

    [Fact]
    public void CycleGraph_ProducesInfoOnly()
    {
        var x = New<CountingNode>("X").Vm;
        var y = New<CountingNode>("Y").Vm;
        var nodes = new List<NodeViewModel> { x, y };
        var conns = new List<ConnectionModel> { Flow(x, y), Flow(y, x) };

        var report = FlowValidator.Analyze(nodes, conns);

        Assert.False(report.HasErrors);
        Assert.Contains(report.Diagnostics, d => d.Code == "I-CYCLE");
    }

    [Fact]
    public void CleanLinearFlow_NoDiagnostics()
    {
        var (a, _) = New<ValueNode>("A");
        var (b, _) = New<ValueNode>("B");
        var nodes = new List<NodeViewModel> { a, b };
        // B 的 IN 由 A 供给；A 作为入口也必须有 IN → 用 StartNode 语义：给 A 接一个已连接的假入边不现实，
        // 这里直接断言：仅存在 IN 缺失错误时 HasErrors 为真，接线后为空。
        var reportWithoutIn = FlowValidator.Analyze(nodes, new List<ConnectionModel> { Flow(a, b) });
        Assert.Equal(1, reportWithoutIn.ErrorCount); // 仅 A 缺 IN

        // 补上 A 的入口连线后完全干净（入口桩节点不勾选 IN）
        var entry = new NodeViewModel(new CountingNode { Name = "ENTRY", IsInEnabled = false });
        nodes.Insert(0, entry);
        var fullConns = new List<ConnectionModel> { Flow(entry, a), Flow(a, b) };
        var reportClean = FlowValidator.Analyze(nodes, fullConns);
        Assert.False(reportClean.HasErrors);
        Assert.DoesNotContain(reportClean.Diagnostics, d => d.Severity == FlowDiagnosticSeverity.Error);
    }
}
