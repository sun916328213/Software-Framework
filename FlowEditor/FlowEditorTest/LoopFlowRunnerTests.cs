using FlowEditor.Core;
using FlowEditor.Nodes;
using Xunit;

namespace FlowEditorTest;

public class LoopFlowRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_RunsBodyExactlyConfiguredNumberOfTimes()
    {
        var model = new CountingNode { Name = "循环体节点" };
        var body = new ParallelFlowLane<NodeModel>(
            "body", "循环体", [model], []);

        await LoopFlowRunner.ExecuteAsync(
            body, 3, CancellationToken.None, _ => { });

        Assert.Equal(3, model.Executions);
        Assert.Equal(3, model.Count);
        Assert.Equal(1, model.CurrentRunCount);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsEmptyBody()
    {
        var body = new ParallelFlowLane<NodeModel>(
            "body", "循环体", [], []);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LoopFlowRunner.ExecuteAsync(body, 2, CancellationToken.None, _ => { }));

        Assert.Contains("没有节点", error.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsConciseIterationAndNodeLogs()
    {
        var node = new CountingNode { Name = "任务" };
        var body = new ParallelFlowLane<NodeModel>("body", "循环体", [node], []);
        var logs = new List<string>();

        await LoopFlowRunner.ExecuteAsync(body, 2, CancellationToken.None, logs.Add);

        Assert.Equal(4, logs.Count(message => message.StartsWith("[循环]")));
        Assert.DoesNotContain(logs, message => message.StartsWith("[引擎]"));
        Assert.DoesNotContain(logs, message => message.Contains("[循环:", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("[任务] [开始]", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("[任务] [完成]", StringComparison.Ordinal));
    }
}
