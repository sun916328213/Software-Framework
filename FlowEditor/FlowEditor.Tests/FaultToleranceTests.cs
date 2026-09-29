using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>P2 验收：节点级容错策略（重试 / 超时 / ContinueOnError / RouteToErrorPort）。</summary>
public class FaultToleranceTests
{
    private readonly TestFlowChartExecutionEngine _engine = new();

    /// <summary>前 FailTimes 次执行抛异常，之后成功。</summary>
    public class FlakyNode : NodeModel
    {
        public int Attempts;
        public int FailTimes = 2;
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Attempts);
            if (Attempts <= FailTimes)
                throw new InvalidOperationException($"flaky #{Attempts}");
            return Task.CompletedTask;
        }
    }

    /// <summary>长时间执行（尊重取消令牌）。</summary>
    public class SlowNode : NodeModel
    {
        public override async Task ExecuteAsync(CancellationToken ct)
            => await Task.Delay(10_000, ct);
    }

    // ── 重试 ──

    [Fact]
    public async Task Retry_SucceedsAfterTransientFailures()
    {
        var flakyModel = new FlakyNode { Name = "F", FailTimes = 2, RetryCount = 3 };
        var flaky = new NodeViewModel(flakyModel);
        var next = New<CountingNode>("N").Vm;
        var nodes = new List<NodeViewModel> { flaky, next };
        var conns = new List<ConnectionModel> { Flow(flaky, next) };

        var logs = new List<string>();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, logs.Add);

        Assert.Equal(3, flakyModel.Attempts);      // 1 次原始 + 2 次重试
        Assert.Equal(NodeStatus.Success, flaky.Status);
        Assert.Equal(1, ((CountingNode)next.Model).Executions);
        Assert.Contains(logs, l => l.Contains("[重试]"));
    }

    [Fact]
    public async Task Retry_Exhausted_FailFast_Throws()
    {
        var flakyModel = new FlakyNode { Name = "F", FailTimes = 100, RetryCount = 2 };
        var flaky = new NodeViewModel(flakyModel);
        var nodes = new List<NodeViewModel> { flaky };

        await Assert.ThrowsAsync<AggregateException>(
            () => _engine.ExecuteAsync(nodes, [], CancellationToken.None, _ => { }));

        Assert.Equal(3, flakyModel.Attempts);          // 1 + 2 次重试
        Assert.Equal(NodeStatus.Error, flaky.Status);
    }

    [Fact]
    public async Task Timeout_FailFast_TerminatesQuickly()
    {
        var slow = new NodeViewModel(new SlowNode { Name = "S", TimeoutMs = 150 });
        var nodes = new List<NodeViewModel> { slow };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => _engine.ExecuteAsync(nodes, [], CancellationToken.None, _ => { }));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 3000, $"应在超时后快速失败（实际 {sw.ElapsedMilliseconds}ms）");
        Assert.Contains(ex.InnerExceptions, e => e is NodeTimeoutException);
    }

    [Fact]
    public async Task Timeout_ParentCancel_NotSwallowedAsTimeout()
    {
        // 父级取消应原样抛 OCE，而不是被包装成超时失败
        var slowModel = new SlowNode { Name = "S" }; // 无超时限制
        var slow = new NodeViewModel(slowModel);
        var nodes = new List<NodeViewModel> { slow };

        using var cts = new CancellationTokenSource(200);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _engine.ExecuteAsync(nodes, [], cts.Token, _ => { }));
    }

    // ── ContinueOnError ──

    [Fact]
    public async Task ContinueOnError_DownstreamStillRuns_NoThrow()
    {
        var a = New<CountingNode>("A").Vm;
        var failModel = new FailNode { Name = "F", ErrorPolicy = NodeErrorPolicy.ContinueOnError };
        var fail = new NodeViewModel(failModel);
        var downstream = New<CountingNode>("C").Vm;
        var nodes = new List<NodeViewModel> { a, fail, downstream };
        var conns = new List<ConnectionModel> { Flow(a, fail), Flow(fail, downstream) };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(NodeStatus.Error, fail.Status);           // 失败节点标记 Error
        Assert.Equal(1, ((CountingNode)downstream.Model).Executions); // 下游照常执行
        Assert.Equal(NodeStatus.Success, downstream.Status);
    }

    // ── RouteToErrorPort ──

    [Fact]
    public async Task RouteToErrorPort_ErrorBranchRuns_NormalBranchPruned()
    {
        var a = New<CountingNode>("A").Vm;
        var failModel = new FailNode { Name = "F", ErrorPolicy = NodeErrorPolicy.RouteToErrorPort };
        var fail = new NodeViewModel(failModel);
        fail.Model.AddCustomOutputPort("Error");

        var normal = New<CountingNode>("N").Vm;     // 正常出边 → 应被剪枝
        var handler = New<CountingNode>("H").Vm;    // Error 出边 → 应被执行

        var nodes = new List<NodeViewModel> { a, fail, normal, handler };
        var conns = new List<ConnectionModel>
        {
            Flow(a, fail),
            Branch(fail, "Next_Out", normal),
            Branch(fail, "Error", handler),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(NodeStatus.Error, fail.Status);
        Assert.Equal(0, ((CountingNode)normal.Model).Executions);
        Assert.Equal(NodeStatus.Idle, normal.Status);
        Assert.Equal(1, ((CountingNode)handler.Model).Executions);
        // Error 端口携带异常消息
        Assert.Contains("boom", (string?)fail.Model.OutputPorts.First(p => p.Name == "Error").Value ?? "");
    }

    [Fact]
    public async Task RouteToErrorPort_Unwired_BehavesAsContinue()
    {
        var a = New<CountingNode>("A").Vm;
        var failModel = new FailNode { Name = "F", ErrorPolicy = NodeErrorPolicy.RouteToErrorPort };
        var fail = new NodeViewModel(failModel);
        var downstream = New<CountingNode>("C").Vm;
        var nodes = new List<NodeViewModel> { a, fail, downstream };
        var conns = new List<ConnectionModel> { Flow(a, fail), Flow(fail, downstream) };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(NodeStatus.Error, fail.Status);
        Assert.Equal(1, ((CountingNode)downstream.Model).Executions);
    }

    [Fact]
    public async Task RouteToErrorPort_InCycle_TerminatesLoopOnError()
    {
        // 环路中的节点失败并路由 Error 后：回边被剪枝，环路自然终止
        var loopModel = new LoopNode { Name = "L", Iterations = 100 }; // 若不失败会跑很久
        loopModel.ErrorPolicy = NodeErrorPolicy.RouteToErrorPort;
        loopModel.AddCustomOutputPort("Error");
        var loop = new NodeViewModel(loopModel);

        var bodyModel = new FailNode { Name = "W", ErrorPolicy = NodeErrorPolicy.RouteToErrorPort };
        bodyModel.AddCustomOutputPort("Error");
        var body = new NodeViewModel(bodyModel);

        var done = New<CountingNode>("D").Vm;
        var nodes = new List<NodeViewModel> { loop, body, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Flow(body, loop),                    // 回边
            Branch(body, "Error", done),         // body 失败 → Error 边退出
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000, "失败路由后环路应立即终止");
        Assert.Equal(NodeStatus.Error, body.Status);   // 首次执行即失败并路由 Error
        Assert.Equal(1, ((CountingNode)done.Model).Executions);
    }

    /// <summary>从 source 名为 portName 的输出端口连到 target 的 Next_IN。</summary>
    private static ConnectionModel Branch(NodeViewModel source, string portName, NodeViewModel target)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = source.Model.OutputPorts.First(p => p.Name == portName).Id,
            TargetNodeId = target.Id,
            TargetPortId = $"{target.Id}_Next_IN"
        };
}
