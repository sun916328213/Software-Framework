using RoslynPad.Editor;

namespace ScriptEditor.Services;

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

    public FixedCodeEditorCompletionProvider(RoslynCodeEditorCompletionProvider original, Func<string>? textProvider = null)
    {
        _original = original;
        _textProvider = textProvider;
    }

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
