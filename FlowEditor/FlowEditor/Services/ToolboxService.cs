using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.ViewModels;

namespace FlowEditor.Services;

/// <summary>
/// 工具箱服务。
/// 统一构建并维护工具箱数据源（NodePalette 与分类视图），
/// 支持搜索过滤、动态排除工具，供容器 ViewModel 与各标签页共享。
/// </summary>
public partial class ToolboxService : ObservableObject
{
    private readonly IFlowToolDiscoveryService _discovery;
    private readonly INodeFactory _nodeFactory;

    /// <summary>全部工具项。</summary>
    public ObservableCollection<NodePaletteItem> Items { get; } = new();

    /// <summary>按分类分组的树形视图。</summary>
    public ObservableCollection<ToolboxCategory> Categories { get; } = new();

    private string _searchText = string.Empty;

    /// <summary>搜索文本，变化时自动重建分类视图。</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                RebuildCategories();
        }
    }

    /// <summary>创建工具箱模块。</summary>
    /// <param name="discovery">工具元数据发现模块。</param>
    /// <param name="nodeFactory">按类型创建节点的 factory。</param>
    public ToolboxService(IFlowToolDiscoveryService discovery, INodeFactory nodeFactory)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
    }

    /// <summary>重新发现工具并构建工具箱。</summary>
    public void Refresh()
    {
        Items.Clear();
        _discovery.Discover();
        foreach (var tool in _discovery.DiscoveredTools)
        {
            Items.Add(new NodePaletteItem(
                tool.DisplayName,
                tool.ToolGroup,
                tool.ToolSubGroup,
                tool.IconKind,
                tool.Description,
                () => _nodeFactory.Create(tool.ToolType)));
        }
        RebuildCategories();
    }

    // 异步加载：首次扫描在后台线程完成，页面立即渲染、工具箱随后填充，不阻塞 UI。
    private Task? _loadingTask;
    private bool _loaded;

    /// <summary>
    /// 异步构建工具箱。首次调用在后台线程执行工具发现与条目构建，
    /// 完成后回到调用线程（UI）填充集合。多次调用共享同一加载任务，不会重复扫描。
    /// </summary>
    public Task RefreshAsync()
    {
        if (_loaded) return Task.CompletedTask;
        return _loadingTask ??= LoadCoreAsync();
    }

    /// <summary>在后台发现工具并在调用上下文恢复后更新工具箱集合。</summary>
    private async Task LoadCoreAsync()
    {
        // 后台：工具发现（若缓存未预热，全程序集反射也在这里完成）+ 构建条目。
        // NodePaletteItem 构造仅保存引用与工厂，不涉及 UI，可安全在后台执行。
        var items = await Task.Run(() =>
        {
            _discovery.Discover();
            var list = new List<NodePaletteItem>();
            foreach (var tool in _discovery.DiscoveredTools)
            {
                list.Add(new NodePaletteItem(
                    tool.DisplayName,
                    tool.ToolGroup,
                    tool.ToolSubGroup,
                    tool.IconKind,
                    tool.Description,
                    () => _nodeFactory.Create(tool.ToolType)));
            }
            return list;
        });

        // 回到 UI 线程后填充集合并重建分类视图。
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
        RebuildCategories();
        _loaded = true;
    }

    /// <summary>从工具箱中排除指定名称的工具（用于 ToolBlock 内部编辑器排除自身）。</summary>
    public void ExcludeToolByName(string toolName)
    {
        var toRemove = Items.Where(p => p.DisplayName == toolName).ToList();
        foreach (var item in toRemove)
            Items.Remove(item);
        RebuildCategories();
    }

    /// <summary>按搜索文本重建分类视图。</summary>
    public void RebuildCategories()
    {
        Categories.Clear();

        var source = string.IsNullOrWhiteSpace(SearchText)
            ? Items
            : new ObservableCollection<NodePaletteItem>(
                Items.Where(p =>
                    p.DisplayName.ToLower().Contains(SearchText.ToLower()) ||
                    p.Description.ToLower().Contains(SearchText.ToLower())));

        foreach (var group in source.GroupBy(p => p.Category))
        {
            var category = new ToolboxCategory { Name = group.Key };
            foreach (var item in group.Where(p => string.IsNullOrEmpty(p.SubCategory)))
                category.Items.Add(item);

            foreach (var sub in group.Where(p => !string.IsNullOrEmpty(p.SubCategory)).GroupBy(p => p.SubCategory))
                category.SubGroups.Add(new ToolboxSubGroup
                {
                    Name = sub.Key,
                    Items = new ObservableCollection<NodePaletteItem>(sub)
                });

            Categories.Add(category);
        }
    }
}
