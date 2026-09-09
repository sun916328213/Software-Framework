using System.Collections.ObjectModel;
using System.Windows;
using FlowEditor.Nodes.FlowControl;

namespace FlowEditor.Views;

/// <summary>条件分发规则的专用编辑窗口。</summary>
public partial class DispatchRuleEditorWindow : Window
{
    private readonly SwitchNode _node;

    public ObservableCollection<DispatchRuleRow> Rules { get; } = new();

    public DispatchRuleEditorWindow(SwitchNode node)
    {
        InitializeComponent();
        _node = node;
        foreach (var (match, branch) in SwitchNode.ParseRules(node.RulesText))
            Rules.Add(new DispatchRuleRow { Match = match, Branch = branch });

        IgnoreCaseCheckBox.IsChecked = node.IgnoreCase;
        TrimInputCheckBox.IsChecked = node.TrimInput;
        DataContext = this;
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var next = Rules.Count + 1;
        Rules.Add(new DispatchRuleRow { Match = next.ToString(), Branch = $"OUT{next}" });
        RulesGrid.SelectedIndex = Rules.Count - 1;
        RulesGrid.ScrollIntoView(Rules[^1]);
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is DispatchRuleRow row)
            Rules.Remove(row);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RulesGrid.CommitEdit();
        RulesGrid.CommitEdit();

        var normalized = Rules
            .Select(row => (Match: row.Match.Trim(), Branch: row.Branch.Trim()))
            .ToList();
        var error = Validate(normalized);
        if (error != null)
        {
            ValidationText.Text = error;
            return;
        }

        _node.ApplyRuleConfiguration(
            normalized,
            IgnoreCaseCheckBox.IsChecked == true,
            TrimInputCheckBox.IsChecked == true);
        DialogResult = true;
    }

    private string? Validate(IReadOnlyList<(string Match, string Branch)> rules)
    {
        if (rules.Count == 0) return "请至少添加一条分发规则。";
        if (rules.Count > 12) return "分发规则最多支持 12 条。";
        if (rules.Any(rule => rule.Match.Length == 0 || rule.Branch.Length == 0))
            return "匹配值和输出分支名称不能为空。";
        if (rules.Any(rule => ContainsSeparator(rule.Match) || ContainsSeparator(rule.Branch)))
            return "匹配值和分支名称不能包含 =、:、; 或换行。";

        var comparison = IgnoreCaseCheckBox.IsChecked == true
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (rules.Select(rule => rule.Match).Distinct(comparison).Count() != rules.Count)
            return "匹配值不能重复。";
        if (rules.Select(rule => rule.Branch).Distinct(StringComparer.Ordinal).Count() != rules.Count)
            return "输出分支名称不能重复。";
        if (rules.Any(rule => rule.Branch == SwitchNode.EmptyBranchName))
            return $"“{SwitchNode.EmptyBranchName}”是保留名称，请使用其他分支名。";
        return null;
    }

    private static bool ContainsSeparator(string value)
        => value.IndexOfAny(['=', ':', ';', '；', '\r', '\n']) >= 0;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

public sealed class DispatchRuleRow
{
    public string Match { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}
