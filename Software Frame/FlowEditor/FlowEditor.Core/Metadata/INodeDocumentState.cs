namespace FlowEditor.Core;

/// <summary>节点显式保存/恢复附加状态的扩展；失败必须上报，不能按反射兼容规则静默跳过。</summary>
public interface INodeDocumentState
{
    /// <summary>捕获纯文档状态，不保存执行结果或原生句柄。</summary>
    Dictionary<string, object?> CaptureDocumentState();
    /// <summary>在候选节点上严格恢复文档状态；不运行生产逻辑。</summary>
    void RestoreDocumentState(Dictionary<string, object?> state);
}
