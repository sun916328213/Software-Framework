// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeStatus。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core.Nodes;

/// <summary>节点执行状态。</summary>
public enum NodeStatus
{
    /// <summary>空闲，未执行。</summary>
    Idle,

    /// <summary>正在执行。</summary>
    Running,

    /// <summary>执行成功。</summary>
    Success,

    /// <summary>执行异常。</summary>
    Error
}
