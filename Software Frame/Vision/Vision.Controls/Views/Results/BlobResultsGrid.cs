using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

#nullable enable
namespace Vision.Controls.View;

/// <summary>为结果表添加所选特征列；直接复用共享DataGridView，不派生带XAML的控件。</summary>
public static class BlobResultsGrid
{
    public static readonly DependencyProperty SelectedFeaturesProperty = DependencyProperty.RegisterAttached("SelectedFeatures",
        typeof(IEnumerable<string>), typeof(BlobResultsGrid), new PropertyMetadata(null, FeaturesChanged));
    public static IEnumerable<string>? GetSelectedFeatures(DependencyObject target) => (IEnumerable<string>?)target.GetValue(SelectedFeaturesProperty);
    public static void SetSelectedFeatures(DependencyObject target, IEnumerable<string>? value) => target.SetValue(SelectedFeaturesProperty, value);
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State",
        typeof(FeatureColumns), typeof(BlobResultsGrid), new PropertyMetadata(null));

    private static void FeaturesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        if (grid.GetValue(StateProperty) is not FeatureColumns state)
        {
            state = new(grid);
            grid.SetValue(StateProperty, state);
            grid.Loaded += (_, _) => state.ObserveFeatures();
            grid.Unloaded += (_, _) => state.StopObserving();
        }
        if (grid.IsLoaded) state.ObserveFeatures();
    }

    private sealed class FeatureColumns(DataGrid grid)
    {
        private INotifyCollectionChanged? _observed;
        private readonly List<DataGridColumn> _featureColumns = [];
        public void StopObserving()
        {
            if (_observed != null) _observed.CollectionChanged -= FeatureSelectionChanged;
            _observed = null;
        }
        public void ObserveFeatures()
        {
            StopObserving();
            _observed = GetSelectedFeatures(grid) as INotifyCollectionChanged;
            if (_observed != null) _observed.CollectionChanged += FeatureSelectionChanged;
            RefreshColumns();
        }
        private void FeatureSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshColumns();
        private void RefreshColumns()
        {
            foreach (var column in _featureColumns) grid.Columns.Remove(column);
            _featureColumns.Clear();
            foreach (var feature in (GetSelectedFeatures(grid) ?? []).Distinct(StringComparer.Ordinal))
            {
                // 面积、外接宽高及中心已有固定列；其余勾选值紧跟尺寸，优先于坐标。
                if (feature is "area" or "row" or "column" or "width" or "height") continue;
                var column = new DataGridTextColumn
                {
                    Header = feature, Width = 110,
                    Binding = new Binding($"FeatureValues[{feature}]") { Mode = BindingMode.OneWay, StringFormat = "0.###", FallbackValue = "—", TargetNullValue = "—" }
                };
                if (grid.TryFindResource("BlobMeasurementText") is Style style) column.ElementStyle = style;
                grid.Columns.Add(column);
                column.DisplayIndex = Math.Min(4 + _featureColumns.Count, grid.Columns.Count - 1);
                _featureColumns.Add(column);
            }
        }
    }
}
