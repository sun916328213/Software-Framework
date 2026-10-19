using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SecsGem.Workbench.Models;

/// <summary>本地配方选择状态；当前配方只能由 Workbench 的手动切换命令改变。</summary>
public partial class RecipeTestModel : ObservableObject
{
    public IDictionary<string, WorkbenchRecipe> Recipes { get; } = WorkbenchRecipeSamples.Create();
    private readonly Dictionary<string, string> _parseErrors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _formattedIds = new(StringComparer.Ordinal);
    public string[] RecipeNames => Recipes.Keys.Concat(_parseErrors.Keys).Concat(_formattedIds).Distinct(StringComparer.Ordinal).ToArray();
    public ObservableCollection<RecipeListRow> RecipeRows { get; } = new();
    public RecipeTestModel() => NotifyRecipesChanged();
    [ObservableProperty] private string _selectedPpid = "DEMO-FAST";
    [ObservableProperty] private string _currentPpid = "";
    [ObservableProperty] private string _status = "请选择配方并手动切换；通信测试在 SECS/GEM 页面。";
    [ObservableProperty] private WorkbenchRecipe? _currentRecipe;

    public bool CanSwitchRecipe(string? ppid) => !string.IsNullOrEmpty(ppid) && Recipes.ContainsKey(ppid) && !_parseErrors.ContainsKey(ppid);

    public void NotifyRecipeReceived(string ppid, string? error)
    {
        if (error is null) _parseErrors.Remove(ppid);
        else _parseErrors[ppid] = error;
        NotifyRecipesChanged();
    }

    public void NotifyFormattedRecipeReceived(string ppid)
    {
        _formattedIds.Add(ppid);
        NotifyRecipesChanged();
    }

    public void NotifyRecipesDeleted(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            _parseErrors.Clear();
            _formattedIds.Clear();
        }
        else foreach (var id in ids)
        {
            _parseErrors.Remove(id);
            _formattedIds.Remove(id);
        }
        NotifyRecipesChanged();
        if (!string.IsNullOrEmpty(CurrentPpid) && !Recipes.ContainsKey(CurrentPpid))
            Status = $"当前快照 {CurrentPpid} 的目录项已删除；不再可选，当前快照未自动切换。";
    }

    // 增量刷新列表，不覆盖当前快照，也不把新增行自动选为待切换配方。
    public void NotifyRecipesChanged()
    {
        var selected = SelectedPpid;
        var ids = RecipeNames;
        foreach (var row in RecipeRows.Where(row => !ids.Contains(row.Key, StringComparer.Ordinal)).ToArray())
            RecipeRows.Remove(row);
        foreach (var id in ids)
        {
            _parseErrors.TryGetValue(id, out var error);
            var row = new RecipeListRow(id, error is null && Recipes.TryGetValue(id, out var recipe) ? recipe : null, error, _formattedIds.Contains(id));
            var index = -1;
            for (var i = 0; i < RecipeRows.Count; i++)
                if (RecipeRows[i].Key == id) { index = i; break; }
            if (index < 0) RecipeRows.Add(row);
            else if (RecipeRows[index] != row) RecipeRows[index] = row;
        }
        SelectedPpid = ids.Contains(selected ?? "", StringComparer.Ordinal) ? selected! : "";
        OnPropertyChanged(nameof(RecipeNames));
    }
}
