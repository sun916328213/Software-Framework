using OxyPlot.Wpf;
using System.Windows;
using WPFControls.Charts.Models;
using WPFControls.Charts.Services.OxyPlot;

namespace WPFControls.Charts.Views.OxyPlot;

/// <summary>Snapshot-based doughnut/pie chart. RefreshChart applies in-place input mutations.</summary>
public sealed class OxyPlotPieView : PlotView
{
    public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
        nameof(ItemSource), typeof(IEnumerable<ChartPieData>), typeof(OxyPlotPieView),
        new PropertyMetadata(null, OnInputChanged));

    public static readonly DependencyProperty InnerDiameterProperty = DependencyProperty.Register(
        nameof(InnerDiameter), typeof(double), typeof(OxyPlotPieView),
        new PropertyMetadata(0.6d, OnInputChanged),
        value => value is double diameter && double.IsFinite(diameter) && diameter >= 0 && diameter < 1);

    public OxyPlotPieView() => RefreshChart();

    public IEnumerable<ChartPieData>? ItemSource
    {
        get => (IEnumerable<ChartPieData>?)GetValue(ItemSourceProperty);
        set => SetValue(ItemSourceProperty, value);
    }

    /// <summary>Inner diameter ratio: zero produces a pie; values below one produce a doughnut.</summary>
    public double InnerDiameter
    {
        get => (double)GetValue(InnerDiameterProperty);
        set => SetValue(InnerDiameterProperty, value);
    }

    /// <summary>Call on the UI thread after editing an existing collection or data item.</summary>
    public void RefreshChart()
    {
        VerifyAccess();
        Model = OxyPlotChartService.CreatePie(ItemSource, InnerDiameter);
    }

    private static void OnInputChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((OxyPlotPieView)sender).RefreshChart();
}
