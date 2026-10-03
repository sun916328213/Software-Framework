// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：INodeContextExecutable。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>
/// 支持 <see cref="NodeExecutionContext"/> 的新执行面（P0）。
/// 引擎优先调用此接口；仅实现 <see cref="INodeExecutable"/> 的旧节点走兼容路径
/// （FillInputPorts → SyncInputValues → ExecuteAsync(ct) → SyncOutputValues），零改动可运行。
/// <para/>
/// 新节点应实现此接口，不再直接接触全局 Container/Manager.Instance、WPF Dispatcher、窗口或具体 SDK 单例；
/// 运行时依赖只从 <see cref="NodeExecutionContext"/> 或构造注入进入。
/// </summary>
public interface INodeContextExecutable
{
    /// <summary>使用统一运行时上下文执行业务逻辑。输入同步与输出同步仍由引擎负责。</summary>
    /// <param name="ctx">由 Runtime 统一创建的执行上下文（含取消令牌、日志、变量、服务、遥测）。</param>
    Task ExecuteAsync(NodeExecutionContext ctx);
}
