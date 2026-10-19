using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using WPFControls.Charts.Models;

namespace WPFControls.Controls.Services.Charts;

/// <summary>Grouped columns. Each shared numeric input describes one data group.</summary>
public static class ChartsColumnService
{
    public static ObservableCollection<ISeries> CreateSeries(IEnumerable<ChartLineData>? data)
    {
        var result = new ObservableCollection<ISeries>();
        foreach (var item in data ?? Enumerable.Empty<ChartLineData>())
        {
            if (item is null) continue;
            var column = new ColumnSeries<double>
            {
                Name = item.SeriesName,
                Values = item.Value ?? new ObservableCollection<double>(),
                Stroke = null
            };
            if (item.Color is { } brush)
                column.Fill = new SolidColorPaint(new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A));
            result.Add(column);
        }
        return result;
    }
}
