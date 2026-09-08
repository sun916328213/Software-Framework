using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFCustomControls.PropertyGrid.ViewModels;

public partial class PropertyGroupViewModel : ObservableObject, IDisposable
{
    public string GroupName { get; }

    /// <summary>本组全部属性行（搜索过滤的母版，生命周期内复用，避免重复订阅/释放）。</summary>
    public List<PropertyItemViewModel> AllItems { get; } = new();

    /// <summary>当前可见属性行（由 <see cref="ApplyFilter"/> 刷新）。</summary>
    public ObservableCollection<PropertyItemViewModel> Items { get; } = new();

    public int ItemCount => Items.Count;
    public bool HasPortConfigurable => Items.Any(i => i.IsPortConfigurable);

    public PropertyGroupViewModel(string groupName)
    {
        GroupName = groupName;
    }

    public void AddItem(PropertyItemViewModel item)
    {
        AllItems.Add(item);
        Items.Add(item);
    }

    /// <summary>按关键字过滤属性行（匹配显示名或属性名）；空关键字显示全部。</summary>
    public void ApplyFilter(string? keyword)
    {
        Items.Clear();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            foreach (var item in AllItems)
                Items.Add(item);
        }
        else
        {
            foreach (var item in AllItems)
            {
                if (item.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.PropertyName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    Items.Add(item);
            }
        }
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(HasPortConfigurable));
    }

    public void Dispose()
    {
        foreach (var item in AllItems)
            item.Dispose();
        AllItems.Clear();
        Items.Clear();
    }
}