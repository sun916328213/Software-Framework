namespace ScriptEditor.Contracts;

/// <summary>
/// 脚本编译阶段状态（运行事实的一部分，由 <see cref="IScriptSession"/> 独占持有）。
/// </summary>
public enum ScriptCompilationState
{
    /// <summary>尚未编译。</summary>
    NotCompiled,

    /// <summary>正在编译。</summary>
    Compiling,

    /// <summary>编译成功（编译缓存有效）。</summary>
    Compiled,

    /// <summary>编译失败。</summary>
    Failed,

    /// <summary>编译或执行被取消。</summary>
    Cancelled
}
