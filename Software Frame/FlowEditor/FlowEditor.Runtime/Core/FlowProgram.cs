// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowProgram。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
using FlowEditor.Core.Models;

namespace FlowEditor.Runtime;

/// <summary>
/// 流程程序：流程图的不可变编译产物（P3 编辑/运行分离的定义层）。
/// 只含纯数据（节点描述符 + 连线），可持久化、可传输、可并发实例化任意多个互不干扰的 FlowInstance。
/// 由画布快照或 .flow 文档编译而来；实例化时才创建真正的节点对象。
/// </summary>
public sealed class FlowProgram
{
    /// <summary>
    /// 初始化 FlowProgram 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    private FlowProgram(
        string name,
        IReadOnlyList<NodeDocumentModel> nodes,
        IReadOnlyList<ConnectionDocumentModel> connections)
    {
        Name = name;
        Nodes = nodes;
        Connections = connections;
    }

    /// <summary>流程名称。</summary>
    public string Name { get; }

    /// <summary>节点描述符集合（类型 + 配置快照，不含运行时状态）。</summary>
    public IReadOnlyList<NodeDocumentModel> Nodes { get; }

    /// <summary>连线集合。</summary>
    public IReadOnlyList<ConnectionDocumentModel> Connections { get; }

    /// <summary>从文档模型编译（文档在此之后被修改不影响本程序）。</summary>
    public static FlowProgram FromDocument(FlowChartDocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new FlowProgram(
            document.Name,
            document.Nodes.ToList(),
            document.Connections.ToList());
    }

    /// <summary>导出为文档模型（用于保存/传输）。</summary>
    public FlowChartDocumentModel ToDocument() => new()
    {
        Name = Name,
        Version = "1.0",
        Nodes = Nodes.ToList(),
        Connections = Connections.ToList()
    };

    /// <summary>编译校验：静态分析报告（错误阻止实例化前的最后防线由调用方决定）。</summary>
    public override string ToString() => $"FlowProgram({Name}, {Nodes.Count} 节点)";
}
