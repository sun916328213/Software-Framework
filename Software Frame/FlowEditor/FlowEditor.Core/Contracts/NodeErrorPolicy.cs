// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeErrorPolicy。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>节点失败处理策略。</summary>
public enum NodeErrorPolicy
{
    /// <summary>默认：节点失败后终止当前流程。</summary>
    FailFast,

    /// <summary>记录错误后继续调度下游节点。</summary>
    ContinueOnError,

    /// <summary>仅激活错误输出端口；未连接时退化为继续执行。</summary>
    RouteToErrorPort
}
