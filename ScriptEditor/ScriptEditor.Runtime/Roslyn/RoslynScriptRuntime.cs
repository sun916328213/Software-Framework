using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ScriptEditor.Contracts;
using ScriptEditor.Models;

namespace ScriptEditor.Services;

/// <summary>
/// 共享 Roslyn 脚本运行时：按脚本数据对象提供 <see cref="ScriptSession"/>，
/// 并对外提供无头编译/执行能力。
/// </summary>
/// <remarks>
/// <para>生命周期约定（P3）：**共享 Roslyn Runtime + 每脚本 Session**。
/// 本类是进程内单例（<see cref="Shared"/>），持有共享 <see cref="CustomRoslynHost"/>（经
/// <see cref="ScriptHostProvider.Shared"/>）；每个 <see cref="ScriptItem"/> 对应一个会话，
/// 由本类按需创建、按需释放。</para>
/// <para>会话以 <see cref="ConditionalWeakTable{TKey,TValue}"/> 关联脚本对象：
/// 脚本对象被回收时会话随之可回收，不会造成长期泄漏。</para>
/// <para>脚本数据对象不持有本类或会话实例，因此「切页」「重新加载任务」不会改变运行事实——
/// 只要脚本对象还是同一个，运行状态就还在同一个会话里。</para>
/// </remarks>
public sealed class RoslynScriptRuntime : IScriptRuntime
{
    /// <summary>进程内共享实例。</summary>
    public static RoslynScriptRuntime Shared { get; } = new();

    private readonly ConditionalWeakTable<ScriptItem, ScriptSession> _itemSessions = new();

    /// <summary>不与脚本对象关联的会话（编辑器文档会话、无头执行会话），需显式释放。</summary>
    private readonly ConcurrentDictionary<ScriptSession, byte> _detachedSessions = new();

    #region 会话获取 / 释放

    /// <summary>获取脚本对应的会话，不存在时创建（幂等）。</summary>
    public IScriptSession GetSession(ScriptItem item)
        => GetOrCreateSession(item);

    /// <summary>查找脚本已存在的会话，不创建。</summary>
    public IScriptSession? FindSession(ScriptItem item)
        => _itemSessions.TryGetValue(item, out var session) ? session : null;

    /// <summary>释放脚本对应的会话（终止执行、停止循环并解除关联）。</summary>
    public void ReleaseSession(ScriptItem item)
    {
        if (!_itemSessions.TryGetValue(item, out var session)) return;
        Shutdown(session);
        _itemSessions.Remove(item);
    }

    /// <summary>释放独立会话。</summary>
    public void ReleaseSession(IScriptSession session)
    {
        if (session is not ScriptSession concrete) return;
        Shutdown(concrete);
        _detachedSessions.TryRemove(concrete, out _);
    }

    /// <summary>创建一个不与脚本数据对象关联的独立会话。</summary>
    public IScriptSession CreateDetachedSession(
        string title,
        Func<string> getCode,
        Func<IReadOnlyList<ScriptReference>>? getReferences = null,
        Func<IEnumerable<int>>? getBreakpoints = null)
    {
        var session = new ScriptSession(getCode, getReferences, getBreakpoints)
        {
            Title = title
        };
        _detachedSessions.TryAdd(session, 0);
        return session;
    }

    private ScriptSession GetOrCreateSession(ScriptItem item)
        => _itemSessions.GetValue(item, static script => new ScriptSession(
            () => script.Code,
            () => script.ScriptReferences,
            () => script.Breakpoints.ToArray(),
            () => script.Title)
        {
            ScriptId = script.ScriptId
        });

    private static void Shutdown(ScriptSession session)
    {
        session.StopLoop();
        session.Terminate();
    }

    #endregion

    #region 无头编译 / 执行

    /// <summary>无头执行一个脚本项目项（使用一次性会话，不经过编辑器）。</summary>
    public async Task<ScriptExecutionResult> ExecuteAsync(
        ScriptProjectItem script, CancellationToken cancellationToken = default)
    {
        var session = CreateDetachedSessionFor(script);
        try
        {
            var success = await session.RunAsync().ConfigureAwait(false);
            return new ScriptExecutionResult(success, session.Output, session.StatusText);
        }
        finally
        {
            ReleaseSession(session);
        }
    }

    /// <summary>无头编译一个脚本项目项（使用一次性会话，不经过编辑器）。</summary>
    public async Task<bool> CompileAsync(
        ScriptProjectItem script, CancellationToken cancellationToken = default)
    {
        var session = CreateDetachedSessionFor(script);
        try
        {
            return await session.CompileAsync().ConfigureAwait(false);
        }
        finally
        {
            ReleaseSession(session);
        }
    }

    private ScriptSession CreateDetachedSessionFor(ScriptProjectItem script)
    {
        var session = new ScriptSession(
            () => script.Code,
            () => script.References,
            () => script.Breakpoints.ToArray())
        {
            ScriptId = script.ScriptId,
            Title = script.Title
        };
        _detachedSessions.TryAdd(session, 0);
        return session;
    }

    #endregion
}
