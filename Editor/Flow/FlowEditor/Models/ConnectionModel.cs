namespace FlowEditor.Models;

/// <summary>连线模型，描述源端口到目标端口的有向连接。</summary>
public class ConnectionModel
{
    /// <summary>连线唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>源节点 ID。</summary>
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>源输出端口 ID。</summary>
    public string SourcePortId { get; set; } = string.Empty;

    /// <summary>目标节点 ID。</summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>目标输入端口 ID。</summary>
    public string TargetPortId { get; set; } = string.Empty;
}
