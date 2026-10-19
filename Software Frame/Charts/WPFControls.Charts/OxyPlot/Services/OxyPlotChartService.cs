using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System.Windows.Media;
using WPFControls.Charts.Models;

namespace WPFControls.Charts.Services.OxyPlot;

/// <summary>Creates independent OxyPlot snapshots from the shared chart input models.</summary>
public static class OxyPlotChartService
{
    public static PlotModel CreateLine(IEnumerable<ChartLineData>? data,
        IEnumerable<string>? labels = null, double axisFontSize = 14)
    {
        if (!double.IsFinite(axisFontSize) || axisFontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(axisFontSize));

        var model = CreateModel();
        var axisLabels = labels?.ToArray();
        if (axisLabels is { Length: > 0 })
        {
            var axis = new CategoryAxis { Position = AxisPosition.Bottom, FontSize = axisFontSize };
            foreach (var label in axisLabels) axis.Labels.Add(label);
            model.Axes.Add(axis);
        }
        else
        {
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, FontSize = axisFontSize });
        }
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, FontSize = axisFontSize });

        foreach (var item in data ?? Enumerable.Empty<ChartLineData>())
        {
            if (item is null) continue;
            var series = new global::OxyPlot.Series.LineSeries { Title = item.SeriesName };
            if (item.Color is not null) series.Color = ConvertColor(item.Color);
            var values = item.Value;
            if (values is null) continue;
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                series.Points.Add(double.IsFinite(value) ? new DataPoint(index, value) : DataPoint.Undefined);
            }
            model.Series.Add(series);
        }
        return model;
    }

    public static PlotModel CreatePie(IEnumerable<ChartPieData>? data, double innerDiameter = 0.6)
    {
        if (!double.IsFinite(innerDiameter) || innerDiameter < 0 || innerDiameter >= 1)
            throw new ArgumentOutOfRangeException(nameof(innerDiameter));

        var model = CreateModel();
        var series = new PieSeries { InnerDiameter = innerDiameter };
        foreach (var item in data ?? Enumerable.Empty<ChartPieData>())
        {
            if (item is null || !double.IsFinite(item.Value) || item.Value <= 0) continue;
            var slice = new PieSlice(item.SeriesName, item.Value);
            if (item.Color is not null) slice.Fill = ConvertColor(item.Color);
            series.Slices.Add(slice);
        }
        model.Series.Add(series);
        return model;
    }

    private static PlotModel CreateModel()
    {
        var model = new PlotModel();
        model.Legends.Add(new Legend { LegendPosition = LegendPosition.BottomCenter });
        return model;
    }

    private static OxyColor ConvertColor(SolidColorBrush brush)
    {
        var color = brush.Color;
        return OxyColor.FromArgb(color.A, color.R, color.G, color.B);
    }
}
