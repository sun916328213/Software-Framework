// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptCompilationState。成员说明可结合下方具体实现阅读。
namespace ScriptEditor.Core.Contracts;

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
