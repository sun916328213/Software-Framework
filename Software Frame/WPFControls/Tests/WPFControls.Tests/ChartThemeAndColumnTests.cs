using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OxyPlot;
using OxyPlot.Wpf;
using SkiaSharp;
using WPFControls.Charts.Models;
using WPFControls.Charts.Themes;
using WPFControls.Charts.Views.OxyPlot;
using WPFControls.Controls.Services.Charts;
using WPFControls.Controls.Views.Charts;
using Xunit;

namespace WPFControls.Tests;

[Collection("Prism module registration")]
public sealed class ChartThemeAndColumnTests
{
    [Theory]
    [InlineData("line")]
    [InlineData("column")]
    [InlineData("pie")]
    [InlineData("gauge")]
    [InlineData("oxyLine")]
    [InlineData("oxyPie")]
    public void InheritedThemeUpdatesNativePaintsAndSurvivesRefresh(string kind) => OnSta(() =>
    {
        var (chart, refresh) = CreateChart(kind);
        var parent = new Grid();
        parent.Children.Add(chart);
        ChartTheme.SetIsDark(parent, true);
        AssertColors(chart, dark: true);
        refresh(); // Replaces native series/model, which must retain the selected theme.
        AssertColors(chart, dark: true);
        ChartTheme.SetIsDark(parent, false);
        AssertColors(chart, dark: false);
        refresh();
        AssertColors(chart, dark: false);
    });

    [Fact]
    public void ThemeIsOptInAndDoesNotOverrideAnUnconfiguredHost() => OnSta(() =>
    {
        var chart = new OxyPlotLineView { Background = Brushes.CadetBlue };
        chart.RefreshChart();
        Assert.Null(ChartTheme.GetIsDark(chart));
        Assert.Same(Brushes.CadetBlue, chart.Background);
    });

    [Fact]
    public void ColumnsPreserveInputGroupsValuesAndColors() => OnSta(() =>
    {
        var input = new ChartLineData
        { SeriesName = "Output", Value = new() { 1, -2, 3 }, Color = Brushes.Teal };
        var result = ChartsColumnService.CreateSeries(new[] { input });
        var series = Assert.IsType<ColumnSeries<double>>(Assert.Single(result));
        Assert.Equal("Output", series.Name);
        Assert.Same(input.Value, series.Values);
        Assert.Equal(new SKColor(0, 128, 128), Assert.IsType<SolidColorPaint>(series.Fill).Color);
        Assert.Empty(ChartsColumnService.CreateSeries(null));
        var chart = new ChartsColumnView { ItemSource = new() { input }, AxisX = new() { "A", "B", "C" } };
        Assert.Equal(3, Assert.IsType<Axis>(Assert.Single(chart.XAxes)).Labels!.Count);
        chart.ItemSource = null;
        Assert.Empty(chart.Series);
    });

    private static void AssertColors(Control chart, bool dark)
    {
        var foreground = dark ? new SKColor(232, 232, 232) : new SKColor(37, 37, 37);
        var background = dark ? Color.FromRgb(32, 33, 36) : Colors.White;
        Assert.Equal(background, Assert.IsType<SolidColorBrush>(chart.Background).Color);
        if (chart is IChartView live)
        {
            Assert.Equal(foreground, Assert.IsType<SolidColorPaint>(live.LegendTextPaint).Color);
            Assert.Equal(foreground, Assert.IsType<SolidColorPaint>(live.TooltipTextPaint).Color);
            if (chart is LiveChartsCore.SkiaSharpView.WPF.CartesianChart cartesian)
                foreach (var axis in cartesian.XAxes.Concat(cartesian.YAxes).OfType<Axis>())
                    Assert.Equal(foreground, Assert.IsType<SolidColorPaint>(axis.LabelsPaint).Color);
        }
        if (chart is PlotView plot)
        {
            Assert.Equal(OxyColor.FromRgb(foreground.Red, foreground.Green, foreground.Blue), plot.Model.TextColor);
            Assert.Equal(OxyColor.FromRgb(background.R, background.G, background.B), plot.Model.Background);
            Assert.All(plot.Model.Axes, axis => Assert.Equal(plot.Model.TextColor, axis.TextColor));
            Assert.All(plot.Model.Legends, legend => Assert.Equal(plot.Model.TextColor, legend.LegendTextColor));
            Assert.NotNull(plot.DefaultTrackerTemplate);
        }
    }

    private static (Control Chart, Action Refresh) CreateChart(string kind)
    {
        var lines = new ObservableCollection<ChartLineData>
        { new() { SeriesName = "工位", Value = new() { 1, 2 }, Color = Brushes.Teal } };
        var pies = new ObservableCollection<ChartPieData>
        { new() { SeriesName = "运行", Value = 50, Color = Brushes.Teal } };
        switch (kind)
        {
            case "line":
                var line = new ChartsLineView { ItemSource = lines, AxisX = new() { "A", "B" } };
                return (line, line.RefreshChart);
            case "column":
                var column = new ChartsColumnView { ItemSource = lines, AxisX = new() { "A", "B" } };
                return (column, column.RefreshChart);
            case "pie":
                var pie = new ChartsPieView { ItemSource = pies };
                return (pie, pie.RefreshChart);
            case "gauge":
                var gauge = new ChartsMultipleGaugeView { ItemSource = pies };
                return (gauge, gauge.RefreshChart);
            case "oxyLine":
                var oxyLine = new OxyPlotLineView { ItemSource = lines };
                return (oxyLine, oxyLine.RefreshChart);
            default:
                var oxyPie = new OxyPlotPieView { ItemSource = pies };
                return (oxyPie, oxyPie.RefreshChart);
        }
    }

    private static void OnSta(Action action)
    {
        ExceptionDispatchInfo? fault = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { fault = ExceptionDispatchInfo.Capture(error); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Chart theme STA test timed out.");
        fault?.Throw();
    }
}
