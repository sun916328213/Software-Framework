// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeTimeoutException。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>节点执行超出 TimeoutMs 限制时抛出。</summary>
public class NodeTimeoutException : Exception
{
    /// <summary>使用超时诊断消息创建异常。</summary>
    /// <param name="message">描述节点及超时限制的消息。</param>
    public NodeTimeoutException(string message) : base(message) { }
}
