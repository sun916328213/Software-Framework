using FlowEditor.Core;

namespace FlowEditor.Interfaces;

/// <summary>
/// 工具箱节点发现服务接口。
/// 反射扫描所有标记 [FlowToolAttribute] 的 NodeModel 子类并缓存元数据。
/// </summary>
public interface IFlowToolDiscoveryService
{
    /// <summary>已发现的所有工具元数据。</summary>
    IReadOnlyList<ToolMetadata> DiscoveredTools { get; }

    /// <summary>扫描当前程序集及已加载程序集，发现所有节点工具。</summary>
    int Discover();

    /// <summary>按分组名称获取分组后的工具列表。</summary>
    IEnumerable<IGrouping<string, ToolMetadata>> GetGroupedTools();

    /// <summary>根据关键词搜索过滤工具。</summary>
    List<ToolMetadata> Search(string keyword);
}
