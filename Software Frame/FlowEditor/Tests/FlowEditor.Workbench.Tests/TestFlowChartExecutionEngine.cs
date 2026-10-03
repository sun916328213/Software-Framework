// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TestFlowChartExecutionEngine。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// 测试图构造适配器。旧测试仍使用 <see cref="NodeViewModel"/> 方便地构造端口和连线，
/// 但所有执行、编译和排序都在此统一转为 <see cref="NodeModel"/>，保证测试走生产的模型执行链。
/// </summary>
internal sealed class TestFlowChartExecutionEngine : FlowChartExecutionEngine
{
    /// <summary>创建使用桌面日志上下文的测试执行引擎。</summary>
    public TestFlowChartExecutionEngine()
        : base(new FlowLogExecutionContextAdapter())
    {
    }

    /// <summary>将 ViewModel 图映射为模型图后执行。</summary>
    public async Task ExecuteAsync(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionModel> connections,
        CancellationToken ct,
        Action<string> log,
        Func<string, Task>? onBeforeNode = null,
        List<NodeViewModel>? executionOrder = null,
        ExecutionJournal? journal = null)
    {
        var models = nodes.Select(node => node.Model).ToList();
        List<NodeModel>? modelOrder = executionOrder == null ? null : [];

        try
        {
            await base.ExecuteAsync(
                models, connections, ct, log, onBeforeNode, modelOrder, journal);
        }
        finally
        {
            if (executionOrder != null && modelOrder != null)
            {
                var viewModelsById = nodes.ToDictionary(node => node.Id);
                executionOrder.AddRange(modelOrder.Select(model => viewModelsById[model.Id]));
            }
        }
    }

    /// <summary>将待单独执行的 ViewModel 及其图映射为模型后执行。</summary>
    public Task ExecuteNodeAsync(
        NodeViewModel node,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionModel> connections,
        CancellationToken ct,
        Action<string> log,
        ExecutionJournal? journal = null) =>
        base.ExecuteNodeAsync(
            node.Model,
            nodes.Select(item => item.Model).ToList(),
            connections,
            ct,
            log,
            journal);

    /// <summary>在模型图上检测旧测试数据是否存在环路。</summary>
    public bool HasCycle(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionModel> connections) =>
        base.HasCycle(nodes.Select(node => node.Model).ToList(), connections);

    /// <summary>在模型图上拓扑排序，再将结果映射回测试 ViewModel。</summary>
    public List<NodeViewModel> TopologicalSort(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionModel> connections) =>
        MapToViewModels(nodes, base.TopologicalSort(
            nodes.Select(node => node.Model).ToList(), connections));

    /// <summary>在模型图上生成拓扑分层，再将结果映射回测试 ViewModel。</summary>
    public List<List<NodeViewModel>> TopologicalLayers(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionModel> connections)
    {
        var layers = base.TopologicalLayers(
            nodes.Select(node => node.Model).ToList(), connections);
        return layers.Select(layer => MapToViewModels(nodes, layer)).ToList();
    }

    /// <summary>按节点 ID 将模型顺序还原为构图时的 ViewModel 顺序。</summary>
    private static List<NodeViewModel> MapToViewModels(
        IReadOnlyList<NodeViewModel> nodes,
        IEnumerable<NodeModel> models)
    {
        var viewModelsById = nodes.ToDictionary(node => node.Id);
        return models.Select(model => viewModelsById[model.Id]).ToList();
    }
}
