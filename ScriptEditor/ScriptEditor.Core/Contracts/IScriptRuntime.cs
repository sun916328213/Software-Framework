using ScriptEditor.Models;

namespace ScriptEditor.Contracts;

/// <summary>一次无头脚本执行的完整结果。</summary>
public sealed record ScriptExecutionResult(bool Success, string Output, string Status);

/// <summary>
/// 脚本运行时：共享的编译/执行服务，按脚本提供 <see cref="IScriptSession"/>。
/// </summary>
/// <remarks>
/// <para>生命周期约定：**共享 Roslyn Runtime + 每脚本 Session**。
/// 运行时（本接口实现）为单例并持有共享 <c>CustomRoslynHost</c>；
/// 每个脚本数据对象对应一个会话，由运行时按需创建与释放。</para>
/// <para>脚本数据对象（<c>ScriptItem</c>）不持有运行时或会话实例，
/// 关联关系只存在于运行时内部。</para>
/// </remarks>
public interface IScriptRuntime
{
    /// <summary>获取脚本对应的会话，不存在时创建（幂等）。</summary>
    IScriptSession GetSession(ScriptItem item);

    /// <summary>查找脚本已存在的会话，不创建；无会话时返回 null。</summary>
    IScriptSession? FindSession(ScriptItem item);

    /// <summary>释放脚本对应的会话（终止执行、停止循环并解除关联）。</summary>
    void ReleaseSession(ScriptItem item);

    /// <summary>释放不再被脚本对象持有的会话（编辑器文档会话等）。</summary>
    void ReleaseSession(IScriptSession session);

    /// <summary>
    /// 创建一个不与脚本数据对象关联的独立会话（编辑器文档场景、无头执行场景）。
    /// 调用方负责在不再需要时调用 <see cref="ReleaseSession(IScriptSession)"/>。
    /// </summary>
    /// <param name="title">会话显示名。</param>
    /// <param name="getCode">获取当前脚本源码。</param>
    /// <param name="getReferences">获取用户 DLL 引用描述（可空）。</param>
    /// <param name="getBreakpoints">获取断点行号集合（可空）。</param>
    IScriptSession CreateDetachedSession(
        string title,
        Func<string> getCode,
        Func<IReadOnlyList<ScriptReference>>? getReferences = null,
        Func<IEnumerable<int>>? getBreakpoints = null);

    /// <summary>无头执行一个脚本项目项（不经过编辑器，使用一次性会话）。</summary>
    Task<ScriptExecutionResult> ExecuteAsync(ScriptProjectItem script, CancellationToken cancellationToken = default);

    /// <summary>无头编译一个脚本项目项（不经过编辑器，使用一次性会话）。</summary>
    Task<bool> CompileAsync(ScriptProjectItem script, CancellationToken cancellationToken = default);
}
