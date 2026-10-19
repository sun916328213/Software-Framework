using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OxyPlot.Series;
using WPFControls.Charts.Views.OxyPlot;
using WPFControls.Charts.Models;
using Xunit;

namespace MachineApp.Host.Tests;

[Collection("ProductionComposition")]
public sealed class OxyPlotPlatformHostTests(ProductionFixture fixture)
{
    [Fact]
    public void OxyPlotViewsLoadFromPublicAssemblyWithoutOverwritingDataContext()
    {
        fixture.Invoke(_ =>
        {
            var grid = (Grid)XamlReader.Parse("""
                <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:charts="clr-namespace:WPFControls.Charts.Views.OxyPlot;assembly=WPFControls.Charts">
                    <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions>
                    <charts:OxyPlotLineView/>
                    <charts:OxyPlotPieView Grid.Column="1"/>
                </Grid>
                """);
            var context = new object();
            grid.DataContext = context;
            var line = Assert.IsType<OxyPlotLineView>(grid.Children[0]);
            var pie = Assert.IsType<OxyPlotPieView>(grid.Children[1]);
            line.AxisX = new[] { "A", "B" };
            line.ItemSource = new[] { new ChartLineData
                { SeriesName = "Output", Value = new() { 1, 2 } } };
            pie.ItemSource = new[] { new ChartPieData { SeriesName = "Pass", Value = 3 } };
            Assert.Same(context, line.DataContext);
            Assert.Same(context, pie.DataContext);
            Assert.Equal("WPFControls.Charts", line.GetType().Assembly.GetName().Name);
            Assert.Equal("WPFControls.Charts", pie.GetType().Assembly.GetName().Name);
            Assert.Equal("Output", Assert.Single(line.Model.Series).Title);
            Assert.Equal("Pass", Assert.Single(Assert.IsType<PieSeries>(Assert.Single(pie.Model.Series)).Slices).Label);

            grid.Measure(new Size(800, 400));
            grid.Arrange(new Rect(0, 0, 800, 400));
            grid.UpdateLayout();
            new RenderTargetBitmap(800, 400, 96, 96, PixelFormats.Pbgra32).Render(grid);
        });
    }

    [Fact]
    public void SnapshotRefreshAndInputReplacementDoNotSharePlotModels()
    {
        fixture.Invoke(_ =>
        {
            var data = new ChartLineData { Value = new() { 1 } };
            var first = new OxyPlotLineView { ItemSource = new[] { data } };
            var second = new OxyPlotLineView { ItemSource = new[] { data } };
            Assert.NotSame(first.Model, second.Model);
            data.Value.Add(2);
            Assert.Single(Assert.IsType<global::OxyPlot.Series.LineSeries>(Assert.Single(first.Model.Series)).Points);
            first.RefreshChart();
            Assert.Equal(2, Assert.IsType<global::OxyPlot.Series.LineSeries>(Assert.Single(first.Model.Series)).Points.Count);
            Assert.Single(Assert.IsType<global::OxyPlot.Series.LineSeries>(Assert.Single(second.Model.Series)).Points);
            first.ItemSource = null;
            Assert.Empty(first.Model.Series);

            var pieData = new ChartPieData { Value = 3 };
            var pie = new OxyPlotPieView { ItemSource = new[] { pieData } };
            pieData.Value = 5;
            pie.RefreshChart();
            Assert.Equal(5d, Assert.Single(Assert.IsType<PieSeries>(Assert.Single(pie.Model.Series)).Slices).Value);
            pie.ItemSource = null;
            Assert.Empty(Assert.IsType<PieSeries>(Assert.Single(pie.Model.Series)).Slices);
        });
    }
}
