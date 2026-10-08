// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：StepExecutionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Services。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Services;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// StepExecutionTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class StepExecutionTests
{
    /// <summary>
    /// 测试相关入口 StepContinueAsync_SelectedNode_ExecutesOnlyThatNode；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、StepContinueAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task StepContinueAsync_SelectedNode_ExecutesOnlyThatNode()
    {
        var service = TestFlowEditorFactory.CreateService();
        var selectedModel = new CountingNode { Name = "Selected" };
        var otherModel = new CountingNode { Name = "Other" };
        var selected = service.Canvas.AddNode(selectedModel);
        service.Canvas.AddNode(otherModel);

        await service.Execution.StepContinueAsync(selected.Model, _ => { });

        Assert.Equal(1, selectedModel.Executions);
        Assert.Equal(0, otherModel.Executions);
    }

    /// <summary>
    /// 测试相关入口 StepContinueAsync_SelectedNode_ReadsCurrentConnectedInputWithoutRunningUpstream；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateService、New、AddNode、Add、Data；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task StepContinueAsync_SelectedNode_ReadsCurrentConnectedInputWithoutRunningUpstream()
    {
        var service = TestFlowEditorFactory.CreateService();
        var (source, sourceModel) = New<SourceNode>("Source");
        var (selected, selectedModel) = New<ValueNode>("Selected");
        service.Canvas.AddNode(source);
        service.Canvas.AddNode(selected);
        service.Connections.Add(Data(source, selected));
        sourceModel.SyncOutputValues();

        await service.Execution.StepContinueAsync(selected.Model, _ => { });

        Assert.Equal(101, selectedModel.Value);
        Assert.Equal(0, sourceModel.Count);
    }

    /// <summary>
    /// 测试相关入口 StepContinueAsync_ExcludedNode_DoesNotExecute；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、StepContinueAsync、Equal、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task StepContinueAsync_ExcludedNode_DoesNotExecute()
    {
        var service = TestFlowEditorFactory.CreateService();
        var model = new CountingNode { Name = "Parked", IsExcludedFromExecution = true };
        var selected = service.Canvas.AddNode(model);
        var logs = new List<string>();

        await service.Execution.StepContinueAsync(selected.Model, logs.Add);

        Assert.Equal(0, model.Executions);
        Assert.Contains(logs, message => message.Contains("不执行节点容器"));
    }
}
