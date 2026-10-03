// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodePaletteItem、ToolboxCategory、ToolboxSubGroup。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Collections.ObjectModel、CommunityToolkit.Mvvm.ComponentModel、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、MaterialDesignThemes.Wpf。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using MaterialDesignThemes.Wpf;

namespace FlowEditor.Controls.ViewModels;

/// <summary>
/// 工具箱节点项描述。
/// 封装节点的显示信息与创建工厂，由 ToolboxService 统一构建。
/// </summary>
public class NodePaletteItem
{
    private readonly Func<NodeModel> _factory;

    /// <summary>工具箱中显示的节点名称。</summary>
    public string DisplayName { get; }
    /// <summary>一级工具分类。</summary>
    public string Category { get; }
    /// <summary>二级工具分类。</summary>
    public string SubCategory { get; }
    /// <summary>工具箱图标。</summary>
    public PackIconKind IconKind { get; }
    /// <summary>节点用途说明。</summary>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ToString。
    /// </remarks>
    public override string ToString() => DisplayName;
}

/// <summary>工具箱 TreeView 分类节点。</summary>
public class ToolboxCategory : ObservableObject
{
    /// <summary>分类显示名称。</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>直接归属于此分类的工具。</summary>
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
