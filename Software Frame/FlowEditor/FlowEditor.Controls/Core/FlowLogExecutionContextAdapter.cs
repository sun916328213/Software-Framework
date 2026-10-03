// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowLogExecutionContextAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core;
using FlowEditor.Runtime;
namespace FlowEditor.Controls.Core;

/// <summary>
/// 将桌面端 <see cref="FlowLogContext"/> 适配为 Runtime 执行引擎所需的日志上下文。
/// </summary>
public sealed class FlowLogExecutionContextAdapter : IExecutionLogContext
{
    /// <inheritdoc />
    public long RunId => FlowLogContext.RunId;

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 EnterNode。
    /// 正文中的关键协作调用包括 EnterNode；具体条件和先后次序见方法体。
    /// </remarks>
    public IDisposable EnterNode(string? nodeName) => FlowLogContext.EnterNode(nodeName);

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 BumpRun。
    /// 正文中的关键协作调用包括 BumpRun；具体条件和先后次序见方法体。
    /// </remarks>
    public void BumpRun() => FlowLogContext.BumpRun();
}
