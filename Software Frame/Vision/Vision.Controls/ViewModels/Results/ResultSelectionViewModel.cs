using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vision.Controls.Presentation;
using Vision.Core.Geometry;
using Vision.Core.Results;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>由工具卡片 Workspace 拥有的结果展示会话；页面和画布只借用。</summary>
public sealed class ResultSelectionViewModel : ObservableObject
{
    private sealed class ResultItems : ObservableCollection<VisionResultItem>
    {
        public void Replace(IReadOnlyList<VisionResultItem> items)
        {
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            OnPropertyChanged(new(nameof(Count)));
            OnPropertyChanged(new("Item[]"));
            OnCollectionChanged(new(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }
    }
    private readonly ResultItems _items = [];
    private VisionResultItem? _selectedItem;
    public ReadOnlyObservableCollection<VisionResultItem> Items { get; }
    public ToolResult? Result { get; private set; }
    public bool HasItems => Items.Count > 0;
    public bool HasMatchItems => Items.Any(item => item.GeometryKind is VisionResultGeometryKind.TemplateRectangle or VisionResultGeometryKind.MatchCenter);
    public bool HasDetectionItems => Items.Any(item => item.GeometryKind == VisionResultGeometryKind.DetectionRectangle);
    public int? SelectedId => SelectedItem?.Id;
    public VisionResultItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            // 旧行不能通过迟到的 SelectionChanged 选中新一次执行里同 ID 的行。
            if (value != null && !_items.Contains(value)) return;
            if (SetProperty(ref _selectedItem, value)) OnPropertyChanged(nameof(SelectedId));
        }
    }
    public ResultSelectionViewModel() => Items = new(_items);

    internal void SetResult(ToolResult? result)
    {
        SelectedItem = null;
        Result = result;
        _items.Replace(VisionDisplay.ResultItems(result));
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasMatchItems));
        OnPropertyChanged(nameof(HasDetectionItems));
    }
    public void SelectId(int? id) => SelectedItem = id.HasValue ? Items.FirstOrDefault(item => item.Id == id.Value) : null;
    public bool SelectAt(Point2D point, double tolerance)
    {
        // 叠加时优先最上层（最后绘制）的结果，不使用表格排序后的行索引。
        var item = Items.LastOrDefault(item => item.HitTest(point, tolerance));
        SelectId(item?.Id);
        return item != null;
    }
}
