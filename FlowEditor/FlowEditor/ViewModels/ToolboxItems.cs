using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Nodes;
using MaterialDesignThemes.Wpf;
using Prism.Ioc;

namespace FlowEditor.ViewModels;

/// <summary>
/// 工具箱节点项描述。
/// 封装节点的显示信息与创建工厂，由 ToolboxService 统一构建。
/// </summary>
public class NodePaletteItem
{
    private readonly IContainerProvider _containerProvider;

    public string DisplayName { get; }
    public string Category { get; }
    public string SubCategory { get; }
    public PackIconKind IconKind { get; }
    public string Description { get; }
    public Func<IContainerProvider, NodeModel> Factory { get; }

    public NodePaletteItem(
        string displayName,
        string category,
        string subCategory,
        PackIconKind iconKind,
        string description,
        Func<IContainerProvider, NodeModel> factory,
        IContainerProvider containerProvider)
    {
        DisplayName = displayName;
        Category = category;
        SubCategory = subCategory;
        IconKind = iconKind;
        Description = description;
        Factory = factory;
        _containerProvider = containerProvider;
    }

    public NodeModel CreateNode() => Factory(_containerProvider);

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
