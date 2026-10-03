// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IExecutionLogContext、NullExecutionLogContext、EmptyScope。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
namespace FlowEditor.Runtime;

/// <summary>
/// 为执行引擎提供宿主日志作用域。Runtime 仅依赖该抽象，桌面端可适配自己的
/// 流程、节点和运行序号上下文，无头宿主则使用默认空实现。
/// </summary>
public interface IExecutionLogContext
{
    /// <summary>获取当前异步执行上下文的运行序号；无头宿主默认为零。</summary>
    long RunId { get; }

    /// <summary>进入指定节点的日志作用域，并在返回对象释放时恢复上一作用域。</summary>
    /// <param name="nodeName">节点显示名称。</param>
    /// <returns>用于退出节点作用域的释放句柄。</returns>
    IDisposable EnterNode(string? nodeName);

    /// <summary>在控制流回边重新调度时推进运行序号。</summary>
    void BumpRun();
}

/// <summary>无头宿主的默认日志上下文，不产生任何外部副作用。</summary>
internal sealed class NullExecutionLogContext : IExecutionLogContext
{
    /// <summary>
    /// EmptyScope 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class EmptyScope : IDisposable
    {
        /// <summary>空作用域无需释放资源。</summary>
        public void Dispose()
        {
        }
    }

    private static readonly IDisposable Scope = new EmptyScope();

    /// <summary>获取共享空实现。</summary>
    public static NullExecutionLogContext Instance { get; } = new();

    /// <inheritdoc />
    public long RunId => 0;

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 EnterNode。
    /// </remarks>
    public IDisposable EnterNode(string? nodeName) => Scope;

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 BumpRun。
    /// </remarks>
    public void BumpRun()
    {
    }
}
