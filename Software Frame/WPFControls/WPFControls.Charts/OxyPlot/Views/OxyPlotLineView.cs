using OxyPlot.Wpf;
using System.Windows;
using WPFControls.Charts.Models;
using WPFControls.Charts.Services.OxyPlot;

namespace WPFControls.Charts.Views.OxyPlot;

/// <summary>Snapshot-based line chart. RefreshChart applies in-place input mutations.</summary>
public sealed class OxyPlotLineView : PlotView
{
    public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
        nameof(ItemSource), typeof(IEnumerable<ChartLineData>), typeof(OxyPlotLineView),
        new PropertyMetadata(null, OnInputChanged));

    public static readonly DependencyProperty AxisXProperty = DependencyProperty.Register(
        nameof(AxisX), typeof(IEnumerable<string>), typeof(OxyPlotLineView),
        new PropertyMetadata(null, OnInputChanged));

    public static readonly DependencyProperty AxisFontSizeProperty = DependencyProperty.Register(
        nameof(AxisFontSize), typeof(double), typeof(OxyPlotLineView),
        new PropertyMetadata(14d, OnInputChanged),
        value => value is double size && double.IsFinite(size) && size > 0);

    public OxyPlotLineView() => RefreshChart();

    public IEnumerable<ChartLineData>? ItemSource
    {
        get => (IEnumerable<ChartLineData>?)GetValue(ItemSourceProperty);
        set => SetValue(ItemSourceProperty, value);
    }

    public IEnumerable<string>? AxisX
    {
        get => (IEnumerable<string>?)GetValue(AxisXProperty);
        set => SetValue(AxisXProperty, value);
    }

    public double AxisFontSize
    {
        get => (double)GetValue(AxisFontSizeProperty);
        set => SetValue(AxisFontSizeProperty, value);
    }

    /// <summary>Call on the UI thread after editing an existing collection or data item.</summary>
    public void RefreshChart()
    {
        VerifyAccess();
        Model = OxyPlotChartService.CreateLine(ItemSource, AxisX, AxisFontSize);
        WPFControls.Charts.Themes.ChartTheme.Apply(this);
    }

    private static void OnInputChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((OxyPlotLineView)sender).RefreshChart();
}
