namespace FlowEditor.Core;

/// <summary>
/// 将桌面端 <see cref="FlowLogContext"/> 适配为 Runtime 执行引擎所需的日志上下文。
/// </summary>
public sealed class FlowLogExecutionContextAdapter : IExecutionLogContext
{
    /// <inheritdoc />
    public long RunId => FlowLogContext.RunId;

    /// <inheritdoc />
    public IDisposable EnterNode(string? nodeName) => FlowLogContext.EnterNode(nodeName);

    /// <inheritdoc />
    public void BumpRun() => FlowLogContext.BumpRun();
}
