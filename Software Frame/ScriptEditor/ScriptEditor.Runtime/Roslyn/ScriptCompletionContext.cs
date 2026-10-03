// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptCompletionContext。成员说明可结合下方具体实现阅读。
namespace ScriptEditor.Runtime.Services;

/// <summary>Identifies preprocessing directive completion from source text and caret position.</summary>
/// <remarks>
/// ScriptCompletionContext 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// </remarks>
public static class ScriptCompletionContext
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 IsDirective。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、IsLetterOrDigit；具体条件和先后次序见方法体。
    /// </summary>
    public static bool IsDirective(string? text, int position, char? triggerChar)
    {
        if (triggerChar == '#') return true;
        if (string.IsNullOrEmpty(text) || position < 0 || position > text.Length) return false;

        int start = position;
        while (start > 0)
        {
            char c = text[start - 1];
            if (c != '#' && c != '_' && !char.IsLetterOrDigit(c)) break;
            start--;
        }
        return start < position && text[start] == '#';
    }
}
