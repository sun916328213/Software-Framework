using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using Xunit;

namespace FlowEditorTest;

public class ParallelFlowRunnerTests
{
    private sealed class ControlledNode : NodeModel
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        public void Complete() => _release.TrySetResult();
    }

    [Fact]
    public async Task ExecuteAsync_StartsAllLanes_AndWaitsForEveryLane()
    {
        var firstModel = new ControlledNode { Name = "分支1" };
        var secondModel = new ControlledNode { Name = "分支2" };
        var lanes = new[]
        {
            new ParallelFlowLane<NodeModel>("lane-1", "分支1", [firstModel], []),
            new ParallelFlowLane<NodeModel>("lane-2", "分支2", [secondModel], [])
        };

        var execution = ParallelFlowRunner.ExecuteAsync(
            lanes, CancellationToken.None, _ => { });

        await Task.WhenAll(firstModel.Started.Task, secondModel.Started.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));

        firstModel.Complete();
        await Task.Delay(30);
        Assert.False(execution.IsCompleted);

        secondModel.Complete();
        await execution.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(NodeStatus.Success, firstModel.Status);
        Assert.Equal(NodeStatus.Success, secondModel.Status);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsFewerThanTwoLanes()
    {
        var node = new CountingNode { Name = "Only" };
        var lanes = new[]
        {
            new ParallelFlowLane<NodeModel>("lane-1", "分支1", [node], [])
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ParallelFlowRunner.ExecuteAsync(lanes, CancellationToken.None, _ => { }));

        Assert.Contains("至少需要两个分支", error.Message);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsEmptyLane()
    {
        var node = new CountingNode { Name = "First" };
        var lanes = new[]
        {
            new ParallelFlowLane<NodeModel>("lane-1", "分支1", [node], []),
            new ParallelFlowLane<NodeModel>("lane-2", "分支2", [], [])
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ParallelFlowRunner.ExecuteAsync(lanes, CancellationToken.None, _ => { }));

        Assert.Contains("分支2", error.Message);
        Assert.Contains("没有节点", error.Message);
    }
}
