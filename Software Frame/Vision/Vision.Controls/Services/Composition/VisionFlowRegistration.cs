#nullable enable
using Prism.Ioc;
using Extension.Integration.Prism;
using Vision.Controls.Services;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using MaterialDesignThemes.Wpf;
using Vision.FlowNode;

namespace Vision.Controls;

/// <summary>复用原流程模块/节点/文档适配。宿主只注入带文件选择等平台回调的目录。</summary>
public static class VisionFlowRegistration
{
    public static void RegisterVisionFlow(this IContainerRegistry registry, Func<VisionBrowserCatalog> createCatalog)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(createCatalog);
        var catalog = new Lazy<VisionBrowserCatalog>(createCatalog);
        registry.RegisterSingleton<WPFCustomControls.PropertyGrid.Core.Services.IPropertyGridBuilderService>(_ =>
        {
            var tools = catalog.Value.Tools;
            return new Vision.FlowNode.VisionNodePropertyGridBuilder(
                node => tools.SingleOrDefault(t => t.Name == node.ToolIdentity)?.NodeParameterNames ?? [],
                node =>
                {
                    if (node.Session is Vision.Controls.Services.VisionToolInstanceWorkspace workspace && workspace.Editor.IsEditingEnabled)
                        workspace.RefreshParameterEditors();
                });
        });
        if (!registry.IsRegistered<FlowEditor.Controls.Discovery.IFlowNodeCatalog>())
            registry.RegisterInstance<FlowEditor.Controls.Discovery.IFlowNodeCatalog>(new FlowEditor.Controls.Discovery.FlowNodeCatalog(
                [typeof(FlowEditor.Controls.Nodes.NodeModel).Assembly, typeof(Vision.FlowNode.VisionToolNodeBase).Assembly]));
        registry.RegisterSingleton<IFlowToolDiscoveryService>(provider => CreateToolDiscovery(catalog.Value,
            provider.Resolve<IFlowNodeCatalog>()));
        registry.RegisterSingleton<VisionNodeEditorHandler>();
        registry.RegisterSingleton<FlowEditor.Controls.Interfaces.INodeEditorLauncher>(provider =>
            new FlowEditor.Controls.Presentation.NodeEditing.WpfNodeEditorLauncher(provider,
                provider.Resolve<IEnumerable<FlowEditor.Controls.Interfaces.INodeEditorHandler>>()
                    .Append(provider.Resolve<VisionNodeEditorHandler>())));
        registry.RegisterSingleton<FlowEditor.Controls.Interfaces.IFlowDocumentStore>(_ => new Vision.FlowNode.VisionTaskDocumentStore(
            new FlowEditor.Controls.Services.Storage.JsonFlowDocumentStore()));
        registry.RegisterSingleton<FlowEditor.Controls.Core.INodeFactory>(provider =>
        {
            var toolCatalog = catalog.Value;
            return new Vision.FlowNode.VisionNodeFactory(config =>
            {
                var entry = toolCatalog.Tools.SingleOrDefault(t => t.Name == config.ToolIdentity)
                    ?? throw new InvalidOperationException($"未知视觉工具：{config.ToolIdentity}");
                if (config.Provider.Length > 0 && !toolCatalog.Providers.Any(p => p.Name == config.Provider))
                    throw new InvalidOperationException("未知Provider。");
                return new Vision.Controls.Services.VisionToolInstanceWorkspace(toolCatalog, entry, config.Provider);
            }, new FlowEditor.Controls.Presentation.Factories.PrismNodeFactory(provider),
                provider.Resolve<FlowEditor.Controls.Discovery.IFlowNodeCatalog>());
        });
        registry.RegisterSingleton<Vision.Controls.ViewModels.VisionFlowViewModel>(provider =>
            new Vision.Controls.ViewModels.VisionFlowViewModel(
                provider.Resolve<FlowEditor.Controls.ViewModels.FlowChartViewModel>(), catalog.Value, true,
                provider.Resolve<ToolboxService>()));
        registry.RegisterNavigationView<Vision.Controls.View.VisionFlowView, Vision.Controls.ViewModels.VisionFlowViewModel>();
    }

    // 程序化创建与Prism宿主共用同一组合，不在ViewModel内复制发现、分类和搜索。
    internal static ToolboxService CreateToolbox(VisionBrowserCatalog catalog)
    {
        var nodes = new FlowNodeCatalog([typeof(NodeModel).Assembly, typeof(VisionToolNodeBase).Assembly]);
        return new(CreateToolDiscovery(catalog, nodes), new ReflectionNodeFactory(null, nodes));
    }

    private static IFlowToolDiscoveryService CreateToolDiscovery(VisionBrowserCatalog catalog, IFlowNodeCatalog nodes)
        => new FlowToolDiscoveryService(nodes, CreateToolRegistrations(catalog), node =>
            // 仅为Vision页面限制新建范围；不从类型目录删除旧节点。
            node.NodeType != typeof(ToolBlockNode) && node.NodeType != typeof(LoopContainerNode) &&
            node.NodeType != typeof(InactiveNodeContainerNode) && node.NodeType != typeof(ParallelForkNode));

    /// <summary>提供可选视觉工具描述；其他宿主可显式合并或筛选，不注册页面、不加载SDK、不启动工具。</summary>
    /// <remarks>宿主仍须组合Vision.FlowNode类型目录和原VisionNodeFactory，才能保存和恢复视觉节点。</remarks>
    public static IReadOnlyList<ToolMetadata> CreateToolRegistrations(VisionBrowserCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Array.AsReadOnly(catalog.Tools.Select((entry, index) => new ToolMetadata
        {
            ToolId = entry.Name,
            ToolType = typeof(VisionToolNodeBase), // 配置工具的公共契约；Factory按工具身份创建具体节点。
            DisplayName = entry.Name,
            ToolGroup = entry.Backend,
            IconKind = PackIconKind.Cog,
            SortIndex = 100 + index,
            Description = $"{entry.Name} · {entry.Backend}；双击添加或拖入流程画布",
            Factory = () =>
            {
                var session = new VisionToolInstanceWorkspace(catalog, entry);
                try { return VisionNodeFactory.CreateToolNode(session); }
                catch (Exception original)
                {
                    // 尚未发布、更未执行的新会话可同步完成清理，不遗失构造异常。
                    try { session.DisposeAsync().GetAwaiter().GetResult(); }
                    catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
                    throw;
                }
            }
        }).ToArray());
    }
}
