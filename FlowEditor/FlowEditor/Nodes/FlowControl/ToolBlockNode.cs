using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 工具块节点：将多个工具封装为一个容器，减少主流程显示。
/// 内部可包含任意数量的其他节点，执行时按序运行。
/// 双击或点击按钮可打开内部流程编辑器查看/编辑内含节点。
/// </summary>
[FlowTool("ToolBlock", "PackageVariantClosed", ToolGroup = "流程控制", SortIndex = 7,
    Description = "工具块：将多个工具封装为一个，减少主流程显示")]
public class ToolBlockNode : NodeModel, IInternalFlowContainerNode
{
    /// <summary>
    /// 内部流程数据（可序列化）。
    /// 存储所有子节点和连线信息，通过 Properties 字典自动序列化。
    /// </summary>
    [NodeProperty(DisplayName = "内部流程数据", Group = "0.容器", Order = 0, IsDisplay = false)]
    public List<FlowChartDocumentModel>? InternalData { get; set; }

    /// <inheritdoc />
    public string InternalEditorTitle => $"[{Name}] 内部流程编辑";

    /// <inheritdoc />
    public IReadOnlyCollection<string> ExcludedInternalTools => ["ToolBlock"];

    /// <inheritdoc />
    public int MinimumInternalFlows => 0;

    /// <inheritdoc />
    public int MaximumInternalFlows => int.MaxValue;

    /// <inheritdoc />
    public string InternalFlowNamePrefix => "流程图";

    /// <summary>创建可承载多个内部流程的工具块节点。</summary>
    public ToolBlockNode()
    {
        Name = "ToolBlock";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    /// <summary>
    /// 打开内部流程可视化编辑器。
    /// </summary>
    [NodeAction("打开内部流程", Order = 0, IconResource = "CodeBraces")]
    public void OpenInternalFlow()
    {
        Service?.NodeEditorLauncher.Open(this);
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        if (InternalData == null || InternalData.Count == 0)
            return;

        var service = Service;
        if (service == null) return;

        // 并行执行所有子流程；每个子流程使用独立调度状态，但继承外层日志上下文。
        var tasks = InternalData.Select(async flowDoc =>
        {
            var engine = ExecutionScopeRuntime?.Engine?.CreateIsolated()
                         ?? new FlowChartExecutionEngine();
            // 重建节点
            var nodeModels = flowDoc.Nodes
                .Select(n =>
                {
                    var model = service.RebuildNodeFromDocument(n);
                    model.AttachTo(service);
                    return model;
                })
                .ToList();

            // 内部流程拥有独立运行图。结构化容器必须从这里取得自己的成员节点，
            // 不能回退读取 ToolBlock 所在的外层画布。
            var connections = flowDoc.Connections
                .Select(connection => new ConnectionModel
                {
                    Id = connection.Id,
                    SourceNodeId = connection.SourceNodeId,
                    SourcePortId = connection.SourcePortId,
                    TargetNodeId = connection.TargetNodeId,
                    TargetPortId = connection.TargetPortId
                })
                .ToList();
            var runtime = new ExecutionScopeRuntime(nodeModels, connections, engine);
            foreach (var node in nodeModels)
                node.ExecutionScopeRuntime = runtime;

            // 与主画布使用同一顶层作用域规则，避免容器成员既由容器执行、
            // 又被内部引擎误判成独立入口额外执行一次。
            var executionGraph = ExecutionGraphScope.CreateTopLevel(nodeModels, connections);
            try
            {
                await engine.ExecuteAsync(
                    executionGraph.Nodes, executionGraph.Connections, ct, _ => { });
            }
            finally
            {
                foreach (var node in nodeModels)
                    node.DetachFromFlow();
            }
        });
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 获取所有子流程的节点总数（用于UI显示）。
    /// </summary>
    public int InternalNodeCount => InternalData?.Sum(f => f.Nodes.Count) ?? 0;
}
