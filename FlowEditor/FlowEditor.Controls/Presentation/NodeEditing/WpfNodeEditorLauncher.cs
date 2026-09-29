using System.Windows;
using FlowEditor.Interfaces;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
using FlowEditor.ViewModels;
using FlowEditor.Views;
using Prism.Ioc;

namespace FlowEditor.Presentation.NodeEditing;

/// <summary>根据节点类型选择并打开 WPF 编辑窗口的 adapter。</summary>
public sealed class WpfNodeEditorLauncher : INodeEditorLauncher
{
    private readonly IContainerProvider _containerProvider;

    /// <summary>创建 WPF 节点编辑器启动 adapter。</summary>
    /// <param name="containerProvider">仅在表现层用于组装编辑窗口依赖的 Prism 容器。</param>
    public WpfNodeEditorLauncher(IContainerProvider containerProvider)
    {
        _containerProvider = containerProvider ?? throw new ArgumentNullException(nameof(containerProvider));
    }

    /// <inheritdoc />
    public void Open(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Window window = node switch
        {
            SwitchNode dispatch => new DispatchRuleEditorWindow(dispatch),
            IInternalFlowContainerNode container when node.Service != null =>
                CreateInternalFlowWindow(container, node.Service),
            IInternalFlowContainerNode => throw new InvalidOperationException(
                $"节点 [{node.Name}] 尚未附加到流程，无法打开内部编辑器。"),
            _ => throw new NotSupportedException(
                $"节点类型 [{node.GetType().Name}] 没有注册专用编辑器。")
        };

        var owner = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(candidate => candidate.IsActive);
        if (owner != window)
            window.Owner = owner;
        window.ShowDialog();
    }

    /// <summary>在组合 adapter 中创建内部流程窗口及其独立依赖。</summary>
    /// <param name="node">内部流程容器节点。</param>
    /// <param name="parentService">父流程会话。</param>
    /// <returns>已完成依赖组装的内部流程窗口。</returns>
    private ToolBlockFlowWindow CreateInternalFlowWindow(
        IInternalFlowContainerNode node,
        IFlowChartService parentService)
        => new(
            node,
            parentService,
            _containerProvider.Resolve<FlowChartContainerViewModel>(),
            _containerProvider.Resolve<FlowChartContainerDocumentService>());
}
