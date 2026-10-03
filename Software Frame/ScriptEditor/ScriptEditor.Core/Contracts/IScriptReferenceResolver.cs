// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：IScriptReferenceResolver。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Core.Models;

namespace ScriptEditor.Core.Contracts;

/// <summary>
/// IScriptReferenceResolver 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// </summary>
public interface IScriptReferenceResolver
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Resolve。
    /// </summary>
    IReadOnlyList<ScriptReference> Resolve(ScriptProject project, ScriptProjectItem script);
}
