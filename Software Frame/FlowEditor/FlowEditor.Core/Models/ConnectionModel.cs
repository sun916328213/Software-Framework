// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ConnectionModel。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core.Models;

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
