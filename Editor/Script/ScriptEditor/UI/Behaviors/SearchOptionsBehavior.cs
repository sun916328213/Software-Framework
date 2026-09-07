using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Reflection;
using System.Diagnostics;
using RoslynPad.Editor;
using ScriptEditor.Views;

namespace ScriptEditor.Behaviors;

/// <summary>
/// 将 SearchReplacePanel 的 CheckBox 选项同步到内部 SearchStrategy
/// </summary>
/// <remarks>
/// 加固：反射成员解析结果按 CheckBox 内容缓存（静态表），避免每次切换都重新反射；
/// 所有候选名均解析失败时输出诊断日志（含 SearchReplacePanel 实际成员列表），
/// 防止 RoslynPad 版本升级后静默失效。
/// </remarks>
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

    /// <summary>已解析的反射成员缓存（key = CheckBox 内容文本）。</summary>
    private static readonly Dictionary<string, MemberInfo?> _memberCache = new();

    /// <summary>反射失败已告警标记（避免重复刷日志）。</summary>
    private static readonly HashSet<string> _warnedKeys = new();

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
        if (content == null) return;
        var value = checkBox.IsChecked == true;

        // 候选字段/属性名（按可能性排序）
        var candidates = content switch
        {
            "匹配大小写" => new[] { "_matchCase", "MatchCase", "matchCase", "IsMatchCase" },
            "全字匹配" => new[] { "_wholeWords", "WholeWords", "wholeWords", "IsWholeWords" },
            "使用正则" => new[] { "_useRegex", "UseRegex", "useRegex", "IsUseRegex" },
            _ => Array.Empty<string>()
        };

        // 优先命中缓存
        if (_memberCache.TryGetValue(content, out var cached))
        {
            if (cached != null)
            {
                SetValue(cached, panel, value);
                return;
            }
        }
        else
        {
            // 首次解析：查找并缓存（找不到也缓存 null，避免重复反射）
            MemberInfo? resolved = null;
            foreach (var name in candidates)
            {
                var field = typeof(SearchReplacePanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (field != null) { resolved = field; break; }
                var prop = typeof(SearchReplacePanel).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (prop != null && prop.CanWrite) { resolved = prop; break; }
            }
            _memberCache[content] = resolved;

            if (resolved != null)
            {
                SetValue(resolved, panel, value);
                return;
            }
        }

        // 解析失败：输出一次性诊断日志
        if (_warnedKeys.Add(content))
        {
            var actualMembers = string.Join(", ", typeof(SearchReplacePanel).GetMembers(
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                .Select(m => m.Name)
                .Where(n => n.Contains(content.Length > 2 ? content.Substring(0, 2) : content, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .Take(20));
            Debug.WriteLine(
                $"[SearchOptionsBehavior] 无法解析 SearchReplacePanel 成员（内容=\"{content}\"，候选={string.Join("/", candidates)}）。" +
                $"疑似成员: {actualMembers}。RoslynPad 版本可能已变更内部实现。");
        }
    }

    private static void SetValue(MemberInfo member, SearchReplacePanel panel, bool value)
    {
        try
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(panel, value);
                    break;
                case PropertyInfo prop when prop.CanWrite:
                    prop.SetValue(panel, value);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SearchOptionsBehavior] 设置成员 {member.Name} 失败: {ex.Message}");
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
