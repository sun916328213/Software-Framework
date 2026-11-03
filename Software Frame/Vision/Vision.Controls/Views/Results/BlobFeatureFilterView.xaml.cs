using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Vision.Controls.Presentation;
using Vision.Controls.ViewModels;

#nullable enable
namespace Vision.Controls.View;

/// <summary>借用同一参数行；仅在加载期间观察范围与结果，不拥有或释放编辑会话。</summary>
public partial class BlobFeatureFilterView : UserControl
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(nameof(Editor),
        typeof(BlobParametersEditorViewModel), typeof(BlobFeatureFilterView), new PropertyMetadata(null, EditorChanged));
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items),
        typeof(IEnumerable<VisionResultItem>), typeof(BlobFeatureFilterView), new PropertyMetadata(null, ItemsChanged));
    public BlobParametersEditorViewModel? Editor { get => (BlobParametersEditorViewModel?)GetValue(EditorProperty); set => SetValue(EditorProperty, value); }
    public IEnumerable<VisionResultItem>? Items { get => (IEnumerable<VisionResultItem>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public ObservableCollection<BlobFeatureFilterRow> Rows { get; } = [];
    private INotifyCollectionChanged? _observedItems;
    private INotifyCollectionChanged? _observedRanges;
    public BlobFeatureFilterView()
    {
        InitializeComponent();
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }
    private static void EditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (BlobFeatureFilterView)d;
        if (view.IsLoaded) view.Connect();
    }
    private static void ItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (BlobFeatureFilterView)d;
        if (!view.IsLoaded) return;
        view.ObserveItems();
        view.UpdateObserved();
    }
    private void Connect()
    {
        Disconnect();
        _observedRanges = Editor?.Ranges;
        if (_observedRanges != null) _observedRanges.CollectionChanged += RangesCollectionChanged;
        ObserveItems();
        RebuildRows();
    }
    private void ObserveItems()
    {
        if (_observedItems != null) _observedItems.CollectionChanged -= ItemsCollectionChanged;
        _observedItems = Items as INotifyCollectionChanged;
        if (_observedItems != null) _observedItems.CollectionChanged += ItemsCollectionChanged;
    }
    private void Disconnect()
    {
        FeatureGrid.CancelEdit(DataGridEditingUnit.Cell);
        FeatureGrid.CancelEdit(DataGridEditingUnit.Row);
        if (_observedRanges != null) _observedRanges.CollectionChanged -= RangesCollectionChanged;
        if (_observedItems != null) _observedItems.CollectionChanged -= ItemsCollectionChanged;
        _observedRanges = null; _observedItems = null;
        ClearRows();
    }
    private void ClearRows()
    {
        foreach (var row in Rows) row.Dispose();
        Rows.Clear();
    }
    private void RangesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildRows();
    private void ItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateObserved();
    private void RebuildRows()
    {
        ClearRows();
        if (Editor is { } editor)
            foreach (var source in editor.Ranges) Rows.Add(new(source));
        UpdateObserved();
    }
    private void UpdateObserved()
    {
        var items = Items?.ToArray() ?? [];
        foreach (var row in Rows)
        {
            var values = items.Where(item => item.FeatureValues.ContainsKey(row.Feature))
                .Select(item => item.FeatureValues[row.Feature]).Where(double.IsFinite).ToArray();
            row.UpdateObserved(values.Length == 0 ? null : values.Min(), values.Length == 0 ? null : values.Max());
        }
    }
}
