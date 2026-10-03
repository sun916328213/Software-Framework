// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IFlowToolDiscoveryService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;

namespace FlowEditor.Controls.Interfaces;

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
