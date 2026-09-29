namespace FlowEditor.Core;

/// <summary>允许结构化节点读取当前完整执行图的运行期能力。</summary>
public interface IExecutionScopeRuntimeAware
{
    /// <summary>获取或设置当前执行会话的完整图快照。</summary>
    ExecutionScopeRuntime? ExecutionScopeRuntime { get; set; }
}
