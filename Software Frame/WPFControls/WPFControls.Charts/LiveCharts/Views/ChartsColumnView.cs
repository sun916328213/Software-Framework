using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using WPFControls.Charts.Models;
using WPFControls.Controls.Services.Charts;

namespace WPFControls.Controls.Views.Charts;

/// <summary>Reusable grouped column chart with the same inputs and refresh contract as line charts.</summary>
public sealed class ChartsColumnView : CartesianChart
{
    private ObservableCollection<ChartLineData>? subscribedSource;

    public ChartsColumnView()
    {
        LegendPosition = LegendPosition.Bottom;
        Loaded += (_, _) => { Attach(ItemSource); RefreshChart(); };
        Unloaded += (_, _) => Attach(null);
        RefreshChart();
    }

    public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
        nameof(ItemSource), typeof(ObservableCollection<ChartLineData>), typeof(ChartsColumnView),
        new PropertyMetadata(null, OnSourceChanged));
    public static readonly DependencyProperty AxisXProperty = DependencyProperty.Register(
        nameof(AxisX), typeof(ObservableCollection<string>), typeof(ChartsColumnView),
        new PropertyMetadata(null, OnSettingsChanged));
    public static readonly DependencyProperty AxisFontSizeProperty = DependencyProperty.Register(
        nameof(AxisFontSize), typeof(double), typeof(ChartsColumnView),
        new PropertyMetadata(14d, OnSettingsChanged),
        value => value is double size && double.IsFinite(size) && size > 0);

    public ObservableCollection<ChartLineData>? ItemSource
    {
        get => (ObservableCollection<ChartLineData>?)GetValue(ItemSourceProperty);
        set => SetValue(ItemSourceProperty, value);
    }
    public ObservableCollection<string>? AxisX
    {
        get => (ObservableCollection<string>?)GetValue(AxisXProperty);
        set => SetValue(AxisXProperty, value);
    }
    public double AxisFontSize
    {
        get => (double)GetValue(AxisFontSizeProperty);
        set => SetValue(AxisFontSizeProperty, value);
    }

    public void RefreshChart()
    {
        VerifyAccess();
        XAxes = new[] { new Axis { Labels = AxisX?.ToArray() ?? Array.Empty<string>(), TextSize = AxisFontSize, MinStep = 1 } };
        YAxes = new[] { new Axis { TextSize = AxisFontSize } };
        Series = ChartsColumnService.CreateSeries(ItemSource);
        global::WPFControls.Charts.Themes.ChartTheme.Apply(this);
    }

    private static void OnSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (ChartsColumnView)target;
        view.Attach(view.IsLoaded ? view.ItemSource : null);
        view.RefreshChart();
    }
    private static void OnSettingsChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        => ((ChartsColumnView)target).RefreshChart();

    private void Attach(ObservableCollection<ChartLineData>? source)
    {
        if (ReferenceEquals(subscribedSource, source)) return;
        if (subscribedSource is not null) subscribedSource.CollectionChanged -= OnCollectionChanged;
        subscribedSource = source;
        if (source is not null) source.CollectionChanged += OnCollectionChanged;
    }
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => RefreshChart();
}
