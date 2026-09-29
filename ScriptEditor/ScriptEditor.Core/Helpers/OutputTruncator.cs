namespace ScriptEditor.Helpers;

/// <summary>
/// 输出文本截断工具，将超长输出限制在合理范围内。
/// </summary>
public static class OutputTruncator
{
    private const int MaxLength = 50 * 1024;
    private const int MaxLines = 200;
    private const int KeepLines = 150;

    /// <summary>
    /// 截断输出文本：限制总长度不超过 50KB，行数不超过 200 行。
    /// 超限时采用滑动窗口策略，保留最后 ~150 行。
    /// </summary>
    public static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (value.Length > MaxLength)
            value = value.Substring(value.Length - MaxLength);

        int lineCount = 0;
        for (int i = 0; i < value.Length; i++)
            if (value[i] == '\n') lineCount++;

        if (lineCount > MaxLines)
        {
            int skipLines = lineCount - KeepLines;
            int trimStart = 0;
            for (int i = 0; i < value.Length && skipLines > 0; i++)
                if (value[i] == '\n') { skipLines--; trimStart = i + 1; }
            value = value.Substring(trimStart);
        }

        return value;
    }
}