// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PropertyGroupViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、CommunityToolkit.Mvvm.ComponentModel。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFCustomControls.PropertyGrid.Core.ViewModels;

/// <summary>
/// PropertyGroupViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：ObservableObject、IDisposable。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class PropertyGroupViewModel : ObservableObject, IDisposable
{
    public string GroupName { get; }

    /// <summary>本组全部属性行（搜索过滤的母版，生命周期内复用，避免重复订阅/释放）。</summary>
    public List<PropertyItemViewModel> AllItems { get; } = new();

    /// <summary>当前可见属性行（由 <see cref="ApplyFilter"/> 刷新）。</summary>
    public ObservableCollection<PropertyItemViewModel> Items { get; } = new();

    public int ItemCount => Items.Count;
    public bool HasPortConfigurable => Items.Any(i => i.IsPortConfigurable);

    /// <summary>
    /// 初始化 PropertyGroupViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PropertyGroupViewModel(string groupName)
    {
        GroupName = groupName;
    }

    /// <summary>
    /// 执行添加操作；对应入口为 AddItem。
    /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose、Clear；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        foreach (var item in AllItems)
            item.Dispose();
        AllItems.Clear();
        Items.Clear();
    }
}