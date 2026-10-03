// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：FixedCodeEditorCompletionProvider。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、ScriptEditor.Runtime.Services、RoslynPad.Editor。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using ScriptEditor.Runtime.Services;
using RoslynPad.Editor;

namespace ScriptEditor.Controls.Services;

/// <summary>
/// 自定义代码完成提供者，包装 RoslynCodeEditorCompletionProvider。
/// 1) 将所有 ICompletionDataEx 项包装为 FixedCompletionData，
///    以修复预处理器指令（#Region 等）的文本替换 Bug。
/// 2) 增加"指令上下文"判定：仅当触发字符为 # 或光标前的当前单词以 # 开头时，
///    才允许预处理器指令参与补全；输入 if/region 等普通字母时不会出现 #if 之类的指令项。
/// </summary>
public class FixedCodeEditorCompletionProvider : ICodeEditorCompletionProvider
{
    private readonly RoslynCodeEditorCompletionProvider _original;
    private readonly Func<string>? _textProvider;

    /// <summary>
    /// 初始化 FixedCodeEditorCompletionProvider 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public FixedCodeEditorCompletionProvider(RoslynCodeEditorCompletionProvider original, Func<string>? textProvider = null)
    {
        _original = original;
        _textProvider = textProvider;
    }

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetCompletionData。
    /// 正文中的关键协作调用包括 GetCompletionData、IsDirective、IsNullOrEmpty、StartsWith、Add；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public async Task<CompletionResult> GetCompletionData(int position, char? triggerChar, bool useSignatureHelp)
    {
        // 在 await 前于调用线程（UI 线程）读取编辑器文本，避免跨线程访问 AvalonEdit 文档
        string? editorText = null;
        try { editorText = _textProvider?.Invoke(); } catch { /* 获取文本失败时按非指令上下文处理 */ }

        var result = await _original.GetCompletionData(position, triggerChar, useSignatureHelp);

        if (result.CompletionData != null && result.CompletionData.Count > 0)
        {
            bool directiveContext = ScriptCompletionContext.IsDirective(editorText, position, triggerChar);

            var wrapped = new List<ICompletionDataEx>(result.CompletionData.Count);
            foreach (var item in result.CompletionData)
            {
                // 非指令上下文时过滤 # 开头的项，防止普通关键字输入时混入预处理器指令
                if (!directiveContext && !string.IsNullOrEmpty(item.Text) && item.Text.StartsWith('#'))
                {
                    continue;
                }

                wrapped.Add(new FixedCompletionData(item, directiveContext));
            }

            return new CompletionResult(wrapped, result.OverloadProvider, result.UseHardSelection);
        }

        return result;
    }

}
