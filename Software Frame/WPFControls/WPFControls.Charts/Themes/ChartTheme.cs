using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using OxyPlot;
using OxyPlot.Wpf;
using SkiaSharp;

namespace WPFControls.Charts.Themes;

/// <summary>Opt-in, inherited chart theme. A null value preserves a host's existing chart styling.</summary>
public static class ChartTheme
{
    private static readonly DependencyProperty TrackerInstalledProperty = DependencyProperty.RegisterAttached(
        "TrackerInstalled", typeof(bool), typeof(ChartTheme), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.RegisterAttached(
        "IsDark", typeof(bool?), typeof(ChartTheme),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits,
            (target, _) => Apply(target)));

    public static bool? GetIsDark(DependencyObject target) => (bool?)target.GetValue(IsDarkProperty);
    public static void SetIsDark(DependencyObject target, bool? value) => target.SetValue(IsDarkProperty, value);

    /// <summary>Reapply after replacing native series, axes or PlotModel. UI thread only.</summary>
    public static void Apply(DependencyObject target)
    {
        target.VerifyAccess();
        if (GetIsDark(target) is not bool dark) return;
        if (target is not Control control || target is not (IChartView or PlotView)) return;
        var text = dark ? SKColor.Parse("#E8E8E8") : SKColor.Parse("#252525");
        var background = dark ? SKColor.Parse("#202124") : SKColors.White;
        var grid = dark ? SKColor.Parse("#56585C") : SKColor.Parse("#D6D6D6");
        control.SetCurrentValue(Control.BackgroundProperty, Brush(background));
        control.SetCurrentValue(Control.ForegroundProperty, Brush(text));

        if (target is IChartView live)
        {
            live.LegendTextPaint = Paint(text);
            live.LegendBackgroundPaint = Paint(background);
            live.TooltipTextPaint = Paint(text);
            live.TooltipBackgroundPaint = Paint(background);
            if (target is CartesianChart cartesian)
            {
                if (cartesian.YAxes?.Any() != true) cartesian.YAxes = new[] { new Axis() };
                foreach (var axis in (cartesian.XAxes ?? Array.Empty<ICartesianAxis>())
                    .Concat(cartesian.YAxes ?? Array.Empty<ICartesianAxis>()).OfType<Axis>())
                {
                    axis.LabelsPaint = Paint(text);
                    axis.NamePaint = Paint(text);
                    axis.SeparatorsPaint = Paint(grid);
                }
            }
            foreach (var series in live.Series ?? Array.Empty<LiveChartsCore.ISeries>())
            {
                switch (series)
                {
                    case LineSeries<double> line: line.DataLabelsPaint = Paint(text); break;
                    case ColumnSeries<double> column: column.DataLabelsPaint = Paint(text); break;
                    // Labels inside colored pie slices retain white text; gauge labels use foreground.
                    case PieSeries<double> pie: pie.DataLabelsPaint = Paint(SKColors.White); break;
                    case PieSeries<ObservableValue> gauge: gauge.DataLabelsPaint = Paint(text); break;
                }
            }
        }
        if (target is PlotView plot && plot.Model is { } model)
        {
            var foreground = Oxy(text);
            model.Background = Oxy(background);
            model.TextColor = foreground;
            model.PlotAreaBorderColor = Oxy(grid);
            foreach (var axis in model.Axes)
            {
                axis.TextColor = foreground;
                axis.TitleColor = foreground;
                axis.AxislineColor = Oxy(grid);
                axis.TicklineColor = Oxy(grid);
                axis.MajorGridlineColor = Oxy(grid);
                axis.MinorGridlineColor = Oxy(grid);
            }
            foreach (var legend in model.Legends)
            {
                legend.LegendTextColor = foreground;
                legend.LegendTitleColor = foreground;
                legend.LegendBackground = Oxy(background);
            }
            if (!(bool)plot.GetValue(TrackerInstalledProperty) &&
                plot.ReadLocalValue(PlotView.DefaultTrackerTemplateProperty) == DependencyProperty.UnsetValue)
            {
                var resources = new ResourceDictionary
                {
                    Source = new Uri("/WPFControls.Charts;component/Themes/ChartTheme.xaml", UriKind.Relative)
                };
                plot.SetCurrentValue(PlotView.DefaultTrackerTemplateProperty, resources["ChartTrackerTemplate"]);
                plot.SetValue(TrackerInstalledProperty, true);
            }
            plot.InvalidatePlot(false);
        }
    }

    private static SolidColorBrush Brush(SKColor color)
    {
        var brush = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }
    private static OxyColor Oxy(SKColor color) => OxyColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    private static SolidColorPaint Paint(SKColor color) => new(color)
    { SKTypeface = SKFontManager.Default.MatchCharacter('汉') };
}
