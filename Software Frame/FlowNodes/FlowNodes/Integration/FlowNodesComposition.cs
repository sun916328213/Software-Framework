// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：FlowNodesComposition。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、FlowEditor.Controls.Discovery、FlowEditor.Controls.Nodes、FlowNodes.Script。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Nodes;
using FlowNodes.Script;

namespace FlowNodes.Integration;

/// <summary>生产与工作台共用的节点来源组合；额外插件由宿主显式提供。</summary>
public static class FlowNodesComposition
{
    /// <summary>建立内建、FlowNodes 与显式插件的统一目录，不执行节点或加载硬件。</summary>
    /// <param name="plugins">宿主允许的附加节点程序集。</param>
    public static IFlowNodeCatalog CreateNodeCatalog(IEnumerable<Assembly>? plugins = null)
        // 内建节点、业务节点和宿主允许的插件共用一个目录，工厂、工具箱与持久化查找使用同一来源。
        // 使用明确程序集集合，不因进程刚好加载了其他程序集就改变可用节点；不执行节点构造后的业务。
        => new FlowNodeCatalog(new[] { typeof(NodeModel).Assembly, typeof(ScriptNode).Assembly }.Concat(plugins ?? []));
}
