using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Reflection;
using RoslynPad.Editor;
using ScriptEditor.Views;
using System.Diagnostics;

namespace ScriptEditor.Behaviors;

/// <summary>
/// 将 SearchReplacePanel 的 CheckBox 选项同步到内部 SearchStrategy
/// </summary>
public static class SearchOptionsBehavior
{
    public static readonly DependencyProperty SyncSearchOptionsProperty =
        DependencyProperty.RegisterAttached(
            "SyncSearchOptions",
            typeof(bool),
            typeof(SearchOptionsBehavior),
            new PropertyMetadata(false, OnSyncSearchOptionsChanged));

    public static bool GetSyncSearchOptions(DependencyObject obj) =>
        (bool)obj.GetValue(SyncSearchOptionsProperty);

    public static void SetSyncSearchOptions(DependencyObject obj, bool value) =>
        obj.SetValue(SyncSearchOptionsProperty, value);

    private static void OnSyncSearchOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CheckBox checkBox && (bool)e.NewValue)
        {
            checkBox.Loaded += (_, _) => SyncToSearchStrategy(checkBox);
            checkBox.Checked += (_, _) => SyncToSearchStrategy(checkBox);
            checkBox.Unchecked += (_, _) => SyncToSearchStrategy(checkBox);
        }
    }

    private static void SyncToSearchStrategy(CheckBox checkBox)
    {
        // 向上找 SearchReplacePanel (RoslynPad 的)
        var panel = FindAncestor<SearchReplacePanel>(checkBox);
        if (panel == null)
            return;

        var content = checkBox.Content?.ToString();
        var value = checkBox.IsChecked == true;
        
        // 尝试多种可能的字段/属性名
        var candidates = content switch
        {
            "匹配大小写" => new[] { "_matchCase", "MatchCase", "matchCase", "IsMatchCase" },
            "全字匹配" => new[] { "_wholeWords", "WholeWords", "wholeWords", "IsWholeWords" },
            "使用正则" => new[] { "_useRegex", "UseRegex", "useRegex", "IsUseRegex" },
            _ => Array.Empty<string>()
        };

        foreach (var name in candidates)
        {
            var field = typeof(SearchReplacePanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(panel, value);
                return;
            }
            var prop = typeof(SearchReplacePanel).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(panel, value);
                return;
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}