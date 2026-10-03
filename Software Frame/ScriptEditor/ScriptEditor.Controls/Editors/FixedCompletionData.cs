// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：FixedCompletionData。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、ScriptEditor.Runtime.Services、System.Windows.Media、ICSharpCode.AvalonEdit.CodeCompletion、ICSharpCode.AvalonEdit.Document、ICSharpCode.AvalonEdit.Editing。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using ScriptEditor.Runtime.Services;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using RoslynPad.Editor;

namespace ScriptEditor.Controls.Services;

/// <summary>
/// 修复 RoslynPad 预处理器指令（#Region、#If 等）补全时的文本替换 Bug。
/// 原 RoslynCompletionData.Complete() 使用 CompletionChange.Span，
/// 对 # 开头的预处理器指令计算错误（Span 不包含 #），导致在光标处插入而非替换已输入文本。
/// 本类接管预处理器指令的文本替换。
/// 仅在指令上下文（输入以 # 开头）时才按指令处理；普通补全一律走原始逻辑，
/// 防止附近无关的 # 字符（十六进制颜色、格式化字符串等）导致误插 # 前缀。
/// </summary>
public class FixedCompletionData : ICompletionDataEx
{
    /// <summary>C# 预处理器指令名集合（小写），用于指令上下文中的显示与判定。</summary>
    private static readonly HashSet<string> DirectiveNames = new(StringComparer.Ordinal)
    {
        "if", "else", "elif", "endif", "define", "undef", "region", "endregion",
        "error", "warning", "line", "nullable", "pragma", "r", "load",
        "checksum", "file", "reset", "hidden", "default", "disable", "restore"
    };

    private readonly ICompletionDataEx _original;
    private readonly bool _directiveContext;

    /// <summary>
    /// 初始化 FixedCompletionData 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public FixedCompletionData(ICompletionDataEx original, bool directiveContext = true)
    {
        _original = original;
        _directiveContext = directiveContext;
    }

    public string Text => _original.Text;

    public object Content
    {
        get
        {
            // 指令上下文中显示带 # 的名称，与实际插入结果保持一致
            if (_directiveContext && TryGetDirectiveDisplayName(out string displayName))
            {
                return displayName;
            }
            return _original.Content;
        }
    }

    public object? Description => _original.Description;

    public ImageSource? Image => _original.Image;

    public double Priority => _original.Priority;

    public bool IsSelected => _original.IsSelected;

    public string SortText => _original.SortText ?? string.Empty;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Complete。
    /// 正文中的关键协作调用包括 ShouldHandlePreprocessorDirective、DoPreprocessorDirectiveCompletion、Complete；具体条件和先后次序见方法体。
    /// </summary>
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
    /// 条件：处于指令上下文，且 Text 以 # 开头，
    /// 或在 completionSegment 附近找到 # 且 # 到补全段之间是连续的指令名单词。
    /// </summary>
    private bool ShouldHandlePreprocessorDirective(TextArea textArea, ISegment completionSegment)
    {
        // 非指令上下文（输入不以 # 开头）一律走原始补全逻辑
        if (!_directiveContext)
        {
            return false;
        }

        // 快速路径：Text 以 # 开头
        if (!string.IsNullOrEmpty(Text) && Text.StartsWith('#'))
        {
            return true;
        }

        // 兼容性路径：Text 不带 #（如 Roslyn 将 Region 作为 Text），
        // 但文档中光标前存在以 # 开头的连续指令名
        if (!string.IsNullOrEmpty(Text))
        {
            int hashPos = FindHashPosition(textArea.Document, completionSegment.Offset);
            if (hashPos >= 0 && IsContiguousDirectiveWord(textArea.Document, hashPos, completionSegment.Offset + completionSegment.Length))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 校验 '#' 与补全段末尾之间全部是单词字符（字母/数字/下划线），
    /// 防止把附近无关的 #（十六进制颜色、格式化字符串等）误判为指令补全。
    /// </summary>
    private static bool IsContiguousDirectiveWord(TextDocument document, int hashPos, int endPos)
    {
        int end = Math.Min(endPos, document.TextLength);
        for (int i = hashPos + 1; i < end; i++)
        {
            char c = document.GetCharAt(i);
            if (c != '_' && !char.IsLetterOrDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 判断当前项在指令上下文中是否应显示为 "#名称" 形式。
    /// </summary>
    private bool TryGetDirectiveDisplayName(out string displayName)
    {
        displayName = GetFirstLine(Text ?? string.Empty).Trim();
        if (displayName.Length == 0 || displayName.StartsWith('#'))
        {
            return false;
        }
        if (DirectiveNames.Contains(displayName.ToLowerInvariant()))
        {
            displayName = "#" + displayName;
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
