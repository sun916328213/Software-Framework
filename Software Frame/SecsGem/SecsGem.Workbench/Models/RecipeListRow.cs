namespace SecsGem.Workbench.Models;

/// <summary>已接收的 PPID 即使无法解析也显示，但不允许切换。</summary>
public sealed record RecipeListRow(string Key, WorkbenchRecipe? Value, string? Error, bool HasFormatted = false)
{
    public bool CanSwitch => Value.HasValue && Error is null;
    public string Status => Error is not null ? "解析失败，不可切换"
        : CanSwitch ? (HasFormatted ? "可切换；含格式化正文" : "可切换")
        : "格式化正文未映射，不可切换";
}
