// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：WpfNodeEditorLauncher。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Nodes.FlowControl。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Controls.Views;
using Prism.Ioc;

namespace FlowEditor.Controls.Presentation.NodeEditing;

/// <summary>根据节点类型选择并打开 WPF 编辑窗口的 adapter。</summary>
public sealed class WpfNodeEditorLauncher : INodeEditorLauncher
{
    private readonly IContainerProvider _containerProvider;
    private readonly IReadOnlyList<INodeEditorHandler> _handlers;

    /// <summary>创建 WPF 节点编辑器启动 adapter。</summary>
    /// <param name="containerProvider">仅在表现层用于组装编辑窗口依赖的 Prism 容器。</param>
    public WpfNodeEditorLauncher(IContainerProvider containerProvider) : this(containerProvider, [])
    {
    }

    /// <summary>创建带有扩展节点编辑 adapter 的 WPF 启动器。</summary>
    /// <param name="containerProvider">组装内建编辑窗口的容器。</param>
    /// <param name="handlers">宿主为扩展节点提供的编辑 adapter。</param>
    public WpfNodeEditorLauncher(IContainerProvider containerProvider, IEnumerable<INodeEditorHandler> handlers)
    {
        _containerProvider = containerProvider ?? throw new ArgumentNullException(nameof(containerProvider));
        _handlers = (handlers ?? throw new ArgumentNullException(nameof(handlers))).ToArray();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Open。
    /// 正文中的关键协作调用包括 ThrowIfNull、TryOpen、CreateInternalFlowWindow、GetType、FirstOrDefault；具体条件和先后次序见方法体。
    /// </remarks>
    public void Open(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        foreach (var handler in _handlers)
            if (handler.TryOpen(node)) return;

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
