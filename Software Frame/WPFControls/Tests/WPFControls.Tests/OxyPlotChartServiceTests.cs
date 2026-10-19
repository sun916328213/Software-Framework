using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Windows.Media;
using WPFControls.Charts.Services.OxyPlot;
using WPFControls.Charts.Models;
using Xunit;


namespace WPFControls.Tests;

public sealed class OxyPlotChartServiceTests
{
    [Fact]
    public void LineUsesExistingDataLabelsAndColorWithoutMutatingInput()
    {
        var color = new SolidColorBrush(Color.FromArgb(128, 12, 34, 56));
        var input = new ChartLineData { SeriesName = "Output", Color = color, Value = new() { 1, -2, 3 } };
        var model = OxyPlotChartService.CreateLine(new[] { input }, new[] { "A", "B", "C" }, 18);

        var series = Assert.IsType<global::OxyPlot.Series.LineSeries>(Assert.Single(model.Series));
        Assert.Equal("Output", series.Title);
        Assert.Equal(new[] { new DataPoint(0, 1), new DataPoint(1, -2), new DataPoint(2, 3) }, series.Points);
        Assert.Equal(OxyColor.FromArgb(128, 12, 34, 56), series.Color);
        var axis = Assert.IsType<CategoryAxis>(model.Axes[0]);
        Assert.Equal(new[] { "A", "B", "C" }, axis.Labels);
        Assert.Equal(18d, axis.FontSize);
        Assert.Equal(new[] { 1d, -2d, 3d }, input.Value);
    }

    [Fact]
    public void MissingLabelsUseNumericAxisAndNonFiniteValuesLeaveGaps()
    {
        var input = new ChartLineData { Value = new() { 1, double.NaN, double.PositiveInfinity, 4 } };
        var model = OxyPlotChartService.CreateLine(new[] { input });
        Assert.IsType<LinearAxis>(model.Axes[0]);
        var series = Assert.IsType<global::OxyPlot.Series.LineSeries>(Assert.Single(model.Series));
        Assert.Equal(4, series.Points.Count);
        Assert.True(double.IsNaN(series.Points[1].Y));
        Assert.True(double.IsNaN(series.Points[2].Y));
        Assert.Equal(new DataPoint(3, 4), series.Points[3]);
    }

    [Fact]
    public void PieFiltersNonPositiveAndNonFiniteValuesAndSupportsDoughnuts()
    {
        var model = OxyPlotChartService.CreatePie(new[]
        {
            new ChartPieData { SeriesName = "Pass", Value = 3, Color = new SolidColorBrush(Colors.Green) },
            new ChartPieData { Value = 0 }, new ChartPieData { Value = -1 },
            new ChartPieData { Value = double.NaN }, new ChartPieData { Value = double.PositiveInfinity }
        });
        var series = Assert.IsType<PieSeries>(Assert.Single(model.Series));
        var slice = Assert.Single(series.Slices);
        Assert.Equal("Pass", slice.Label);
        Assert.Equal(3d, slice.Value);
        Assert.Equal(OxyColor.FromArgb(255, 0, 128, 0), slice.Fill);
        Assert.Equal(0.6, series.InnerDiameter);
        Assert.Equal(0d, Assert.IsType<PieSeries>(Assert.Single(
            OxyPlotChartService.CreatePie(null, 0).Series)).InnerDiameter);
    }

    [Fact]
    public void NullInputsProduceIndependentEmptyModels()
    {
        var first = OxyPlotChartService.CreateLine(null);
        var second = OxyPlotChartService.CreateLine(null);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Axes[0], second.Axes[0]);
        Assert.Empty(first.Series);
        Assert.Empty(Assert.IsType<PieSeries>(Assert.Single(OxyPlotChartService.CreatePie(null).Series)).Slices);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidFontSizesAreRejected(double size)
        => Assert.Throws<ArgumentOutOfRangeException>(() => OxyPlotChartService.CreateLine(null, axisFontSize: size));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidInnerDiametersAreRejected(double diameter)
        => Assert.Throws<ArgumentOutOfRangeException>(() => OxyPlotChartService.CreatePie(null, diameter));
}
