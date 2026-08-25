using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using RoslynPad.Editor;

namespace ScriptEditor.Services;

/// <summary>
/// 修复 RoslynPad 预处理器指令（#Region、#If 等）补全时的文本替换 Bug。
/// 原 RoslynCompletionData.Complete() 使用 CompletionChange.Span，
/// 对 # 开头的预处理器指令计算错误（Span 不包含 #），导致在光标处插入而非替换已输入文本。
/// 本类接管预处理器指令的文本替换。
/// </summary>
public class FixedCompletionData : ICompletionDataEx
{
    private readonly ICompletionDataEx _original;

    public FixedCompletionData(ICompletionDataEx original)
    {
        _original = original;
    }

    public string Text => _original.Text;

    public object Content => _original.Content;

    public object? Description => _original.Description;

    public ImageSource? Image => _original.Image;

    public double Priority => _original.Priority;

    public bool IsSelected => _original.IsSelected;

    public string SortText => _original.SortText ?? string.Empty;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs e)
    {
        if (ShouldHandlePreprocessorDirective(textArea, completionSegment))
        {
            DoPreprocessorDirectiveCompletion(textArea, completionSegment);
        }
        else
        {
            _original.Complete(textArea, completionSegment, e);
        }
    }

    /// <summary>
    /// 判断是否应该由我们处理预处理器指令补全。
    /// 条件：Text 以 # 开头，或在 completionSegment 附近找到 # 字符。
    /// </summary>
    private bool ShouldHandlePreprocessorDirective(TextArea textArea, ISegment completionSegment)
    {
        // 快速路径：Text 以 # 开头
        if (!string.IsNullOrEmpty(Text) && Text.StartsWith('#'))
            return true;

        // 兼容性路径：Text 不以 # 开头（如 Roslyn 将 Region 作为 Text），
        // 但在文档中 completionSegment 附近存在 # 字符
        if (!string.IsNullOrEmpty(Text))
        {
            int hashPos = FindHashPosition(textArea.Document, completionSegment.Offset);
            if (hashPos >= 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 执行预处理器指令的补全：从 '#' 字符位置开始替换，确保覆盖已输入的全部文本。
    /// </summary>
    private void DoPreprocessorDirectiveCompletion(TextArea textArea, ISegment completionSegment)
    {
        var document = textArea.Document;
        int offset = completionSegment.Offset;
        int length = completionSegment.Length;

        int hashPos = FindHashPosition(document, offset);

        if (hashPos >= 0 && hashPos <= offset + length)
        {
            int endPos = offset + length;
            string completionText = GetFirstLine(Text!);

            // 如果补全文本不以 # 开头（Roslyn 可能将 Region 作为 Text），
            // 则在补全文本前加上 #，确保替换后结果正确
            if (!completionText.StartsWith('#'))
            {
                completionText = "#" + completionText;
            }

            document.BeginUpdate();
            try
            {
                document.Replace(hashPos, endPos - hashPos, new StringTextSource(completionText));
            }
            finally
            {
                document.EndUpdate();
            }

            textArea.Caret.Offset = hashPos + completionText.Length;
        }
        else
        {
            _original.Complete(textArea, completionSegment, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 获取文本的第一行（去除换行符及后续内容）。
    /// Roslyn 的 #Region 补全项可能包含 #endregion 及换行符。
    /// </summary>
    private static string GetFirstLine(string text)
    {
        int newlineIdx = text.IndexOf('\n');
        if (newlineIdx >= 0)
        {
            return text.Substring(0, newlineIdx).TrimEnd('\r', '\n');
        }
        return text;
    }

    /// <summary>
    /// 从指定位置向前查找最近的 '#' 字符。
    /// 同时检查 offset 位置本身，以兼容 AvalonEdit 将 # 包含在 completionSegment 中的情况。
    /// </summary>
    private static int FindHashPosition(TextDocument document, int offset)
    {
        int searchStart = Math.Max(0, offset - 20);
        // 修复：从 offset 开始向前搜索（而非 offset+5），避免越过当前行末的换行符导致 break
        int searchEnd = Math.Min(document.TextLength - 1, offset);

        // 先检查 offset 附近（含 offset 本身和之后几个字符）
        for (int i = searchEnd; i >= searchStart; i--)
        {
            char c = document.GetCharAt(i);
            if (c == '#')
            {
                // '#' 前必须是行首或空白字符
                if (i == 0 || char.IsWhiteSpace(document.GetCharAt(i - 1)))
                {
                    return i;
                }
            }
            if (c == '\n' || c == '\r') break;
        }

        return -1;
    }
}
