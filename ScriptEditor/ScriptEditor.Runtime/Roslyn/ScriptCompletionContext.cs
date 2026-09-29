namespace ScriptEditor.Services;

/// <summary>Identifies preprocessing directive completion from source text and caret position.</summary>
public static class ScriptCompletionContext
{
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
