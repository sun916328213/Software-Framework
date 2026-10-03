// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IExecutionScopeRuntimeAware。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
namespace FlowEditor.Runtime;

/// <summary>允许结构化节点读取当前完整执行图的运行期能力。</summary>
public interface IExecutionScopeRuntimeAware
{
    /// <summary>获取或设置当前执行会话的完整图快照。</summary>
    ExecutionScopeRuntime? ExecutionScopeRuntime { get; set; }
}
