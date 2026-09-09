using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Services;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

public class StepExecutionTests
{
    [Fact]
    public async Task StepContinueAsync_SelectedNode_ExecutesOnlyThatNode()
    {
        var service = new FlowChartService(null!, new FlowChartExecutionEngine(), null!);
        var selectedModel = new CountingNode { Name = "Selected" };
        var otherModel = new CountingNode { Name = "Other" };
        var selected = service.AddNode(selectedModel);
        service.AddNode(otherModel);

        await service.StepContinueAsync(selected, _ => { });

        Assert.Equal(1, selectedModel.Executions);
        Assert.Equal(0, otherModel.Executions);
    }

    [Fact]
    public async Task StepContinueAsync_SelectedNode_ReadsCurrentConnectedInputWithoutRunningUpstream()
    {
        var service = new FlowChartService(null!, new FlowChartExecutionEngine(), null!);
        var (source, sourceModel) = New<SourceNode>("Source");
        var (selected, selectedModel) = New<ValueNode>("Selected");
        service.AddNode(source);
        service.AddNode(selected);
        service.Connections.Add(Data(source, selected));
        sourceModel.SyncOutputValues();

        await service.StepContinueAsync(selected, _ => { });

        Assert.Equal(101, selectedModel.Value);
        Assert.Equal(0, sourceModel.Count);
    }

    [Fact]
    public async Task StepContinueAsync_ExcludedNode_DoesNotExecute()
    {
        var service = new FlowChartService(null!, new FlowChartExecutionEngine(), null!);
        var model = new CountingNode { Name = "Parked", IsExcludedFromExecution = true };
        var selected = service.AddNode(model);
        var logs = new List<string>();

        await service.StepContinueAsync(selected, logs.Add);

        Assert.Equal(0, model.Executions);
        Assert.Contains(logs, message => message.Contains("不执行节点容器"));
    }
}
