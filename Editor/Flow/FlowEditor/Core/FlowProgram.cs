using FlowEditor.Models;

namespace FlowEditor.Core;

/// <summary>
/// 流程程序：流程图的不可变编译产物（P3 编辑/运行分离的定义层）。
/// 只含纯数据（节点描述符 + 连线），可持久化、可传输、可并发实例化任意多个互不干扰的 FlowInstance。
/// 由画布快照或 .flow 文档编译而来；实例化时才创建真正的节点对象。
/// </summary>
public sealed class FlowProgram
{
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
