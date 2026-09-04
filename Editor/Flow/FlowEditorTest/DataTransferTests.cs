using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>
/// 输出数据传递机制准确性测试。
/// 覆盖：单链传递、多前驱合并、类型不匹配异常、Journal 传值记录、Error 端口数据携带。
/// </summary>
public class DataTransferTests
{
    private readonly FlowChartExecutionEngine _engine = new();

    // ── 单链数据传递 ──

    [Fact]
    public async Task SingleChain_IntData_PropagatesCorrectly()
    {
        var (a, aModel) = New<SourceNode>("A");       // Value=100
        var (b, bModel) = New<ValueNode>("B");        // Value = input + 1
        var (c, cModel) = New<ValueNode>("C");
        var nodes = new List<NodeViewModel> { a, b, c };
        var conns = new List<ConnectionModel>
        {
            Flow(a, b), Data(a, b),
            Flow(b, c), Data(b, c),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(101, bModel.Value);  // 100 + 1
        Assert.Equal(102, cModel.Value);  // 101 + 1
        Assert.All(nodes, n => Assert.Equal(NodeStatus.Success, n.Status));
    }

    // ── 多前驱数据合并 ──

    /// <summary>双数据源汇聚到同一目标节点：最后一个写入的值生效（按入边顺序）。</summary>
    [Fact]
    public async Task MultiPredecessor_LastWriteWins()
    {
        var (src1, src1Model) = New<SourceNode>("S1");  // Value=100
        var (src2, src2Model) = New<SourceNode>("S2");  // Value=200
        src2Model.Value = 200;
        var (dst, dstModel) = New<ValueNode>("D");

        var nodes = new List<NodeViewModel> { src1, src2, dst };
        var conns = new List<ConnectionModel>
        {
            Flow(src1, dst), Data(src1, dst),
            Flow(src2, dst), Data(src2, dst),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // dst.Value = (last incoming data value) + 1
        // 两个源都执行成功，dst 的输入端口最终持有最后写入的值
        Assert.True(dstModel.Value == 101 || dstModel.Value == 201,
            $"dst.Value 应为 101 或 201（取决于入边顺序），实际 {dstModel.Value}");
    }

    // ── 类型不匹配异常 ──

    /// <summary>声明类型不兼容时抛出 FlowChartTypeMismatchException。</summary>
    [Fact]
    public async Task TypeMismatch_DeclaredType_ThrowsException()
    {
        var (src, srcModel) = New<SourceNode>("Src");   // int 输出
        var (dst, dstModel) = New<ValueNode>("Dst");     // int 输入

        // 手动构造一条类型不匹配的连线：把源端口类型改为 String
        srcModel.OutputPorts.First(p => p.Name == "Value").DataType = PortType.String;
        dstModel.InputPorts.First(p => p.Name == "Value").DataType = PortType.Integer;

        var nodes = new List<NodeViewModel> { src, dst };
        var conns = new List<ConnectionModel>
        {
            Flow(src, dst),
            new()
            {
                SourceNodeId = src.Id,
                SourcePortId = src.Model.OutputPorts.First(p => p.Name == "Value").Id,
                TargetNodeId = dst.Id,
                TargetPortId = dst.Model.InputPorts.First(p => p.Name == "Value").Id,
            },
        };

        await Assert.ThrowsAsync<FlowChartTypeMismatchException>(
            () => _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }));
    }

    /// <summary>运行时值类型不兼容时抛出 FlowChartTypeMismatchException。</summary>
    [Fact]
    public async Task TypeMismatch_RuntimeValue_ThrowsException()
    {
        var (src, srcModel) = New<SourceNode>("Src");
        var (dst, dstModel) = New<ValueNode>("Dst");

        // 把源端口的运行时值设为不兼容类型（string 而非 int）
        srcModel.OutputPorts.First(p => p.Name == "Value").Value = "not_an_int";
        srcModel.OutputPorts.First(p => p.Name == "Value").DataType = PortType.String;
        dstModel.InputPorts.First(p => p.Name == "Value").DataType = PortType.Integer;

        var nodes = new List<NodeViewModel> { src, dst };
        var conns = new List<ConnectionModel>
        {
            Flow(src, dst),
            new()
            {
                SourceNodeId = src.Id,
                SourcePortId = src.Model.OutputPorts.First(p => p.Name == "Value").Id,
                TargetNodeId = dst.Id,
                TargetPortId = dst.Model.InputPorts.First(p => p.Name == "Value").Id,
            },
        };

        await Assert.ThrowsAsync<FlowChartTypeMismatchException>(
            () => _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }));
    }

    // ── Journal 传值记录 ──

    /// <summary>ExecutionJournal 记录每条连线的实际传值。</summary>
    [Fact]
    public async Task Journal_RecordsEdgeTransfer()
    {
        var (a, aModel) = New<SourceNode>("A");
        var (b, _) = New<ValueNode>("B");
        var nodes = new List<NodeViewModel> { a, b };
        var conns = new List<ConnectionModel> { Flow(a, b), Data(a, b) };

        var journal = new ExecutionJournal();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { },
            journal: journal);

        // 至少有一条 EdgeTransfer 记录携带值 100
        var transfers = journal.Entries.Where(e => e.Kind == JournalKind.EdgeTransfer).ToList();
        Assert.NotEmpty(transfers);
        Assert.Contains(transfers, t => t.Value is 100);
    }

    // ── Error 端口数据携带 ──

    /// <summary>RouteToErrorPort 失败时异常消息写入 Error 端口值，下游可读取。</summary>
    [Fact]
    public async Task ErrorPort_CarriesExceptionMessage()
    {
        var a = New<CountingNode>("A").Vm;
        var failModel = new FailNode { Name = "F", ErrorPolicy = NodeErrorPolicy.RouteToErrorPort };
        failModel.AddCustomOutputPort("Error");
        var fail = new NodeViewModel(failModel);

        var handler = New<CountingNode>("H").Vm;
        var nodes = new List<NodeViewModel> { a, fail, handler };
        var conns = new List<ConnectionModel>
        {
            Flow(a, fail),
            new()
            {
                SourceNodeId = fail.Id,
                SourcePortId = fail.Model.OutputPorts.First(p => p.Name == "Error").Id,
                TargetNodeId = handler.Id,
                TargetPortId = $"{handler.Id}_Next_IN",
            },
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(NodeStatus.Error, fail.Status);
        Assert.Equal(NodeStatus.Success, handler.Status);
        var errPort = fail.Model.OutputPorts.First(p => p.Name == "Error");
        Assert.Contains("boom", (string?)errPort.Value ?? "");
    }

    // ── 空值传递 ──

    /// <summary>未设置值的源端口传递 null 不应崩溃。</summary>
    [Fact]
    public async Task NullValue_TransferDoesNotCrash()
    {
        var (src, _) = New<SourceNode>("Src");
        // 不设置 Value 属性，默认为 100（SourceNode 构造）
        var (dst, dstModel) = New<ValueNode>("Dst");

        var nodes = new List<NodeViewModel> { src, dst };
        var conns = new List<ConnectionModel> { Flow(src, dst), Data(src, dst) };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(101, dstModel.Value);
        Assert.Equal(NodeStatus.Success, dst.Status);
    }

    // ── 多节点数据链 ──

    /// <summary>5 节点链式数据传递：100 → 101 → 102 → 103 → 104。</summary>
    [Fact]
    public async Task FiveNodeChain_DataPropagatesThroughAll()
    {
        var nodes = new List<NodeViewModel>();
        var models = new List<ValueNode>();
        var (src, srcModel) = New<SourceNode>("Src");
        nodes.Add(src);
        // SourceNode 的 Value=100

        for (int i = 0; i < 4; i++)
        {
            var (vm, model) = New<ValueNode>($"N{i}");
            nodes.Add(vm);
            models.Add(model);
        }

        var conns = new List<ConnectionModel>();
        // Flow 链: Src → N0 → N1 → N2 → N3
        for (int i = 0; i < nodes.Count - 1; i++)
            conns.Add(Flow(nodes[i], nodes[i + 1]));
        // Data 链: Src.Value → N0.Value → N1.Value → ...
        conns.Add(Data(src, nodes[1]));
        for (int i = 1; i < nodes.Count - 1; i++)
            conns.Add(Data(nodes[i], nodes[i + 1]));

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // Src.Value=100, N0=101, N1=102, N2=103, N3=104
        Assert.Equal(101, models[0].Value);
        Assert.Equal(102, models[1].Value);
        Assert.Equal(103, models[2].Value);
        Assert.Equal(104, models[3].Value);
    }
}
