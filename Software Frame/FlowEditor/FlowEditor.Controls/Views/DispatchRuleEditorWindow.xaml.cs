// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：DispatchRuleEditorWindow、DispatchRuleRow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Collections.ObjectModel、System.Windows、FlowEditor.Controls.Nodes.FlowControl。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Collections.ObjectModel;
using System.Windows;
using FlowEditor.Controls.Nodes.FlowControl;

namespace FlowEditor.Controls.Views;

/// <summary>条件分发规则的专用编辑窗口。</summary>
public partial class DispatchRuleEditorWindow : Window
{
    private readonly SwitchNode _node;

    /// <summary>窗口中正在编辑的分发规则行。</summary>
    public ObservableCollection<DispatchRuleRow> Rules { get; } = new();

    /// <summary>从指定条件分发节点加载可编辑规则。</summary>
    /// <param name="node">接收已验证规则配置的条件分发节点。</param>
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

    /// <summary>添加一条带默认匹配值和输出名的规则。</summary>
    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var next = Rules.Count + 1;
        Rules.Add(new DispatchRuleRow { Match = next.ToString(), Branch = $"OUT{next}" });
        RulesGrid.SelectedIndex = Rules.Count - 1;
        RulesGrid.ScrollIntoView(Rules[^1]);
    }

    /// <summary>删除规则表格中当前选中的规则。</summary>
    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is DispatchRuleRow row)
            Rules.Remove(row);
    }

    /// <summary>提交表格编辑，验证规则并写回条件分发节点。</summary>
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

    /// <summary>验证规则数量、必填值、分隔符、唯一性和保留名称。</summary>
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

    /// <summary>判断值中是否包含规则文本使用的结构分隔符。</summary>
    private static bool ContainsSeparator(string value)
        => value.IndexOfAny(['=', ':', ';', '；', '\r', '\n']) >= 0;

    /// <summary>关闭窗口且不写回规则修改。</summary>
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

/// <summary>分发规则编辑表格中的单行数据。</summary>
public sealed class DispatchRuleRow
{
    /// <summary>用于匹配输入值的文本。</summary>
    public string Match { get; set; } = string.Empty;
    /// <summary>匹配成功后激活的输出分支名称。</summary>
    public string Branch { get; set; } = string.Empty;
}
