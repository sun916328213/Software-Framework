// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ToolMetadata、FlowToolDiscoveryService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core、FlowEditor.Runtime、System.Reflection、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core;
using FlowEditor.Runtime;
using System.Reflection;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using MaterialDesignThemes.Wpf;
using FlowEditor.Controls.Discovery;

namespace FlowEditor.Controls.Core;

/// <summary>
/// 工具箱工具元数据缓存。
/// </summary>
public class ToolMetadata
{
    /// <summary>节点类型；配置工具可声明公共契约，由Factory创建实际具体类型。</summary>
    public Type ToolType { get; set; } = null!;

    /// <summary>工具项稳定身份；允许同一节点契约公开多个配置工具，不使用显示名去重。</summary>
    public string ToolId { get; init; } = string.Empty;

    /// <summary>可选配置节点工厂；只在添加时调用，发现和搜索不创建实例。</summary>
    public Func<NodeModel>? Factory { get; init; }

    /// <summary>显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>分组名称。</summary>
    public string ToolGroup { get; set; } = "常规";

    /// <summary>子分组名称（为空表示直接挂在一级分组下）。</summary>
    public string ToolSubGroup { get; set; } = string.Empty;

    /// <summary>图标。</summary>
    public PackIconKind IconKind { get; set; }

    /// <summary>排序权重。</summary>
    public int SortIndex { get; set; }

    /// <summary>描述。</summary>
    public string Description { get; set; } = string.Empty;

}

/// <summary>
/// 工具箱节点发现服务。
/// 程序启动时反射扫描所有标记 [FlowToolAttribute] 的 NodeModel 子类，
/// 提取元数据缓存，支持动态发现和搜索过滤。
/// </summary>
public class FlowToolDiscoveryService : IFlowToolDiscoveryService
{
    private readonly object _discoveryLock = new();
    private List<ToolMetadata> _allTools = new();
    private bool _discovered;
    private readonly IFlowNodeCatalog _catalog;
    private readonly IReadOnlyList<ToolMetadata> _additionalTools;
    private readonly Func<FlowNodeDescriptor, bool> _isVisible;

    /// <summary>兼容直接构造，使用默认程序集来源的独立目录。</summary>
    public FlowToolDiscoveryService() : this(new FlowNodeCatalog()) { }

    /// <summary>使用宿主共享的节点目录，工具箱不再自行枚举程序集。</summary>
    /// <param name="catalog">宿主的节点描述目录。</param>
    public FlowToolDiscoveryService(IFlowNodeCatalog catalog) : this(catalog, []) { }

    /// <summary>将模块显式提供的配置工具与反射元数据合并；可见性策略不影响文档类型解析。</summary>
    public FlowToolDiscoveryService(IFlowNodeCatalog catalog, IEnumerable<ToolMetadata> additionalTools,
        Func<FlowNodeDescriptor, bool>? isVisible = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        ArgumentNullException.ThrowIfNull(additionalTools);
        _additionalTools = Array.AsReadOnly(additionalTools.ToArray());
        _isVisible = isVisible ?? (_ => true);
    }

    /// <summary>已发现的所有工具元数据。</summary>
    public IReadOnlyList<ToolMetadata> DiscoveredTools => _allTools;

    /// <summary>
    /// 扫描当前程序集及已加载程序集，发现所有 [FlowTool] 节点工具。
    /// 首次扫描结果缓存，后续调用直接返回，避免每个工具箱实例重复反射扫描。
    /// 线程安全：同一时间只有一个线程执行扫描，其余调用等待缓存就绪。
    /// </summary>
    /// <returns>发现的工具数量。</returns>
    public int Discover()
    {
        lock (_discoveryLock)
        {
            if (_discovered) return _allTools.Count;

            var tools = new List<ToolMetadata>();
            foreach (var node in _catalog.Nodes.Where(node => node.VisibleInToolbox && node.DisplayName != null && _isVisible(node)))
            {
                var iconKind = PackIconKind.CubeOutline;
                if (!string.IsNullOrEmpty(node.IconResource))
                {
                    try { iconKind = (PackIconKind)Enum.Parse(typeof(PackIconKind), node.IconResource); }
                    catch { iconKind = PackIconKind.CubeOutline; }
                }
                tools.Add(new ToolMetadata
                {
                    ToolType = node.NodeType,
                    ToolId = node.NodeTypeId ?? node.NodeType.FullName!,
                    DisplayName = node.DisplayName!,
                    ToolGroup = node.ToolGroup,
                    ToolSubGroup = node.ToolSubGroup,
                    IconKind = iconKind,
                    SortIndex = node.SortIndex,
                    Description = node.Description
                });
            }

            // 同一描述重复装配是幂等的；不同描述不得静默抢占同一身份。
            tools.AddRange(_additionalTools);
            tools = tools.Distinct().ToList();
            if (tools.Any(tool => string.IsNullOrWhiteSpace(tool.ToolId)))
                throw new InvalidOperationException("工具注册必须提供稳定ToolId。");
            var duplicate = tools.GroupBy(tool => tool.ToolId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
                throw new InvalidOperationException($"重复工具身份：{duplicate.Key}。");

            // 按 SortIndex 升序排序，同权重按显示名排序
            tools.Sort((a, b) =>
            {
                var cmp = a.SortIndex.CompareTo(b.SortIndex);
                if (cmp != 0) return cmp;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });

            _allTools = tools;
            _discovered = true;
            return tools.Count;
        }
    }

    /// <summary>
    /// 按分组名称获取分组后的工具列表。
    /// </summary>
    public IEnumerable<IGrouping<string, ToolMetadata>> GetGroupedTools()
        => _allTools.GroupBy(t => t.ToolGroup);

    /// <summary>
    /// 根据关键词搜索过滤工具（支持名称和描述模糊匹配）。
    /// </summary>
    public List<ToolMetadata> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return _allTools.ToList();
        var kw = keyword.ToLower();
        return _allTools.Where(t =>
            t.DisplayName.ToLower().Contains(kw) ||
            t.Description.ToLower().Contains(kw)).ToList();
    }
}
