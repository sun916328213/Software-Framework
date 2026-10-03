// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptReferenceKind、ScriptReference。成员说明可结合下方具体实现阅读。
namespace ScriptEditor.Core.Models;

/// <summary>
/// ScriptReferenceKind 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// </summary>
public enum ScriptReferenceKind
{
    AssemblyFile,
    FrameworkAssembly
}

/// <summary>A reference described without loading Roslyn or accessing the file system.</summary>
/// <remarks>
/// ScriptReference 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// </remarks>
public sealed record ScriptReference(string Path, string Name, ScriptReferenceKind Kind = ScriptReferenceKind.AssemblyFile);
