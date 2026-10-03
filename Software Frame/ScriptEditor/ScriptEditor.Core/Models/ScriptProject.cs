// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptProject、ScriptProjectItem。成员说明可结合下方具体实现阅读。
namespace ScriptEditor.Core.Models;

/// <summary>Stable script project data, independent of editor and runtime state.</summary>
/// <remarks>
/// ScriptProject 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// </remarks>
public sealed class ScriptProject
{
    public string Name { get; set; } = "新建任务";
    public List<ScriptReference> References { get; set; } = new();
    public List<ScriptProjectItem> Scripts { get; set; } = new();
}

/// <summary>
/// ScriptProjectItem 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// </summary>
public sealed class ScriptProjectItem
{
    private Guid _scriptId = Guid.NewGuid();
    public Guid ScriptId
    {
        get => _scriptId;
        set => _scriptId = value == Guid.Empty ? Guid.NewGuid() : value;
    }
    public string Title { get; set; } = "新脚本";
    public string Code { get; set; } = string.Empty;
    public List<ScriptReference> References { get; set; } = new();
    public List<int> Breakpoints { get; set; } = new();
}
