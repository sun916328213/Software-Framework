// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowChartViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Nodes.FlowControl。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;

namespace FlowEditor.Controls.ViewModels;

/// <summary>包含流程运行、停止、调试继续及失败恢复编排。</summary>
public partial class FlowChartViewModel
{
    /// <summary>运行当前流程并把执行日志写入输出栏。</summary>
    private async Task RunAsync()
    {
        await _execution.RunAsync(AppendLog, debugMode: false);
    }

    /// <summary>判断当前流程是否满足运行条件。</summary>
    private bool CanRun() => !Model.IsRunning && !Model.IsLoopRunning && Nodes.Count > 0;

    /// <summary>请求停止当前流程。</summary>
    private void Stop() => _execution.Stop();

    /// <summary>判断当前流程是否可以停止。</summary>
    private bool CanStop() => Model.IsRunning;

    /// <summary>继续调试或从当前选中节点恢复执行。</summary>
    private async Task StepContinueSelectedAsync()
    {
        // 保留调试运行中的“继续”语义；普通状态下执行当前选中节点一次。
        if (Model.IsPaused)
        {
            _execution.StepContinue();
            return;
        }

        var selected = SelectedNode;
        if (selected == null)
            return;

        // 流程中存在失败状态时，“继续”应采用恢复/跳转语义：所选节点成为新入口，
        // 可选择失败节点重试，也可选择它之前或之后的节点继续整条可达路径。
        if (Nodes.Any(node => node.Status == NodeStatus.Error))
            await _execution.ContinueFromNodeAsync(selected.Model, AppendLog);
        else
            await _execution.StepContinueAsync(selected.Model, AppendLog);
    }

    /// <summary>判断当前选择和运行状态是否允许继续执行。</summary>
    private bool CanStepContinue()
        => Model.IsPaused ||
           (!Model.IsRunning && SelectedNode is { IsExcludedFromExecution: false });

    /// <summary>从右键选中的节点恢复或跳转，并继续执行其后继路径。</summary>
    private Task ContinueFromNodeAsync(NodeViewModel? node)
        => node == null ? Task.CompletedTask : _execution.ContinueFromNodeAsync(node.Model, AppendLog);

    /// <summary>空闲时允许任意执行节点跳转；运行中只允许恢复并行容器内的独立支路。</summary>
    private bool CanContinueFromNode(NodeViewModel? node)
        => node is { IsExcludedFromExecution: false } &&
           (!Model.IsRunning ||
            (node.Model.ParentExecutionContainerId is { } parentId &&
             _canvas.FindNode(parentId)?.Model is ParallelContainerNode));

    /// <summary>循环运行当前流程并把执行日志写入输出栏。</summary>
    private async Task LoopRunAsync()
    {
        await _execution.RunLoopAsync(AppendLog);
    }

    /// <summary>判断当前流程是否可以进入循环运行。</summary>
    private bool CanLoopRun() => !Model.IsRunning && !Model.IsLoopRunning && Nodes.Count > 0;

    /// <summary>请求停止当前流程的循环运行。</summary>
    private void LoopStop() => _execution.StopLoop();

    /// <summary>判断当前循环运行是否可以停止。</summary>
    private bool CanLoopStop() => Model.IsLoopRunning;
}
