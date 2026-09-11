using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Nodes;
using MaterialDesignThemes.Wpf;

namespace FlowEditor.ViewModels;

/// <summary>
/// 工具箱节点项描述。
/// 封装节点的显示信息与创建工厂，由 ToolboxService 统一构建。
/// </summary>
public class NodePaletteItem
{
    private readonly Func<NodeModel> _factory;

    public string DisplayName { get; }
    public string Category { get; }
    public string SubCategory { get; }
    public PackIconKind IconKind { get; }
    public string Description { get; }
    /// <summary>创建工具箱节点项。</summary>
    /// <param name="displayName">显示名称。</param>
    /// <param name="category">一级分类。</param>
    /// <param name="subCategory">二级分类。</param>
    /// <param name="iconKind">图标。</param>
    /// <param name="description">工具说明。</param>
    /// <param name="factory">创建独立节点实例的 factory。</param>

    public NodePaletteItem(
        string displayName,
        string category,
        string subCategory,
        PackIconKind iconKind,
        string description,
        Func<NodeModel> factory)
    {
        DisplayName = displayName;
        Category = category;
        SubCategory = subCategory;
        IconKind = iconKind;
        Description = description;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>创建该工具对应的新节点实例。</summary>
    public NodeModel CreateNode() => _factory();

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

/// <summary>工具箱 TreeView 分类节点。</summary>
public class ToolboxCategory : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<NodePaletteItem> Items { get; set; } = new();

    /// <summary>二级子分组列表。</summary>
    public ObservableCollection<ToolboxSubGroup> SubGroups { get; set; } = new();

    /// <summary>分类下工具总数（含子分组）。</summary>
    public int TotalCount => Items.Count + SubGroups.Sum(g => g.Items.Count);

    private bool _isExpanded = true;
    /// <summary>分类是否展开。</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

/// <summary>工具箱二级子分组节点。</summary>
public class ToolboxSubGroup : ToolboxCategory
{
}
