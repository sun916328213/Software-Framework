using System.Reflection;
using FlowEditor.Interfaces;
using FlowEditor.Nodes;
using MaterialDesignThemes.Wpf;

namespace FlowEditor.Core;

/// <summary>
/// 工具箱工具元数据缓存。
/// </summary>
public class ToolMetadata
{
    /// <summary>节点类型。</summary>
    public Type ToolType { get; set; } = null!;

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
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !a.FullName!.StartsWith("System.") && !a.FullName!.StartsWith("Microsoft."))
                .ToList();

            foreach (var asm in assemblies)
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        if (!type.IsAbstract && type.IsSubclassOf(typeof(NodeModel)))
                        {
                            // P2：同步注册稳定节点标识（无标记的类型跳过），供流程加载时解析。
                            NodeTypeRegistry.Register(type);
                            var attr = type.GetCustomAttribute<FlowToolAttribute>();
                            if (attr == null || !attr.VisibleInToolbox) continue;

                            var iconKind = PackIconKind.CubeOutline;
                            if (!string.IsNullOrEmpty(attr.IconResource))
                            {
                                try { iconKind = (PackIconKind)Enum.Parse(typeof(PackIconKind), attr.IconResource); }
                                catch { iconKind = PackIconKind.CubeOutline; }
                            }

                            tools.Add(new ToolMetadata
                            {
                                ToolType = type,
                                DisplayName = attr.ToolName,
                                ToolGroup = attr.ToolGroup,
                                ToolSubGroup = attr.ToolSubGroup,
                                IconKind = iconKind,
                                SortIndex = attr.SortIndex,
                                Description = attr.Description
                            });
                        }
                    }
                }
                catch { /* 跳过无法加载类型的程序集 */ }
            }

            // 按 SortIndex 升序排序，同权重按类名字母排序
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
