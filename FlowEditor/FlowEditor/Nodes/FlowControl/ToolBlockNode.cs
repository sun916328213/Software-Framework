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

    public string InternalEditorTitle => $"[{Name}] 内部流程编辑";

    public IReadOnlyCollection<string> ExcludedInternalTools => ["ToolBlock"];

    public int MinimumInternalFlows => 0;

    public int MaximumInternalFlows => int.MaxValue;

    public string InternalFlowNamePrefix => "流程图";

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
        Service?.OpenInternalFlowEditor(this);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        if (InternalData == null || InternalData.Count == 0)
            return;

        var service = Service;
        if (service == null) return;

        // 并行执行所有子流程
        var engine = new FlowChartExecutionEngine();
        var tasks = InternalData.Select(async flowDoc =>
        {
            // 重建节点
            var nodeModels = flowDoc.Nodes
                .Select(n =>
                {
                    var model = service.RebuildNodeFromDocument(n);
                    model.Service = service;
                    model.OnAttached(service);
                    return model;
                })
                .ToList();

            // 执行
            await engine.ExecuteAsync(nodeModels, flowDoc.Connections, ct, _ => { });
        });
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 获取所有子流程的节点总数（用于UI显示）。
    /// </summary>
    public int InternalNodeCount => InternalData?.Sum(f => f.Nodes.Count) ?? 0;
}
