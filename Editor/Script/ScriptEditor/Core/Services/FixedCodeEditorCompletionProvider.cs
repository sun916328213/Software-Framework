using RoslynPad.Editor;

namespace ScriptEditor.Services;

/// <summary>
/// 自定义代码完成提供者，包装 RoslynCodeEditorCompletionProvider。
/// 将所有 ICompletionDataEx 项包装为 FixedCompletionData，
/// 以修复预处理器指令（#Region 等）的文本替换 Bug。
/// </summary>
public class FixedCodeEditorCompletionProvider : ICodeEditorCompletionProvider
{
    private readonly RoslynCodeEditorCompletionProvider _original;

    public FixedCodeEditorCompletionProvider(RoslynCodeEditorCompletionProvider original)
    {
        _original = original;
    }

    public async Task<CompletionResult> GetCompletionData(int position, char? triggerChar, bool useSignatureHelp)
    {
        var result = await _original.GetCompletionData(position, triggerChar, useSignatureHelp);

        if (result.CompletionData != null && result.CompletionData.Count > 0)
        {
            var wrapped = new List<ICompletionDataEx>(result.CompletionData.Count);
            foreach (var item in result.CompletionData)
            {
                wrapped.Add(new FixedCompletionData(item));
            }

            return new CompletionResult(wrapped, result.OverloadProvider, result.UseHardSelection);
        }

        return result;
    }
}