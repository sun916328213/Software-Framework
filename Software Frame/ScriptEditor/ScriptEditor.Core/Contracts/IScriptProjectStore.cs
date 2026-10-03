// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：IScriptProjectStore。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Core.Models;

namespace ScriptEditor.Core.Contracts;

/// <summary>
/// IScriptProjectStore 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// </summary>
public interface IScriptProjectStore
{
    /// <summary>
    /// 执行加载操作；对应入口为 LoadAsync。
    /// </summary>
    Task<ScriptProject> LoadAsync(string path, CancellationToken cancellationToken = default);
    /// <summary>
    /// 执行保存操作；对应入口为 SaveAsync。
    /// </summary>
    Task SaveAsync(string path, ScriptProject project, CancellationToken cancellationToken = default);
}
