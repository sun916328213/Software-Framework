using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp.Views.WPF;
using WPFControls.Controls.Views.Charts;
using Xunit;

namespace MachineApp.Host.Tests;

[Collection("ProductionComposition")]
public sealed class ChartsPlatformHostTests(ProductionFixture fixture)
{
    [Fact]
    public void ProductionChartRendersThroughModernSkiaWpfSurface()
    {
        fixture.Invoke(container =>
        {
            var chart = new ChartsLineView
            {
                Series = new[] { new LineSeries<double> { Values = new[] { 1d, 3d, 2d } } },
                AnimationsSpeed = TimeSpan.Zero
            };
            Assert.Equal("WPFControls.Charts", chart.GetType().Assembly.GetName().Name);
            var window = new Window { Content = chart, Width = 600, Height = 400 };
            try
            {
                window.Show();
                window.UpdateLayout();
                var surface = Assert.Single(Descendants(chart).OfType<SKElement>());
                var painted = false;
                surface.PaintSurface += (_, args) => painted = args.Info.Width > 0 && args.Info.Height > 0;
                surface.InvalidateVisual();
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!painted && DateTime.UtcNow < deadline)
                {
                    var frame = new DispatcherFrame();
                    _ = chart.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                    Dispatcher.PushFrame(frame);
                    Thread.Sleep(1);
                }
                Assert.True(painted, "Actual Skia WPF surface did not paint.");
                Assert.Contains(".NETCoreApp,Version=v8.0", typeof(SKElement).Assembly
                    .GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                    .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single().FrameworkName);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ExtractedChartXamlLoadsWithExistingDataProperties()
    {
        fixture.Invoke(_ =>
        {
            var line = new ChartsLineView
            {
                AxisX = new() { "A", "B" },
                ItemSource = new()
                {
                    new WPFControls.Charts.Models.ChartLineData
                    { SeriesName = "Output", Value = new() { 1, 2 } }
                }
            };
            var pie = new ChartsPieView
            {
                ItemSource = new()
                {
                    new WPFControls.Charts.Models.ChartPieData { SeriesName = "Pass", Value = 3 }
                }
            };
            var gauge = new ChartsMultipleGaugeView
            {
                ItemSource = new()
                {
                    new WPFControls.Charts.Models.ChartPieData { SeriesName = "Run", Value = 50 }
                }
            };
            Assert.Equal("Output", Assert.Single(line.Series).Name);
            Assert.Equal("Pass", Assert.Single(pie.Series).Name);
            Assert.Contains(gauge.Series, series => series.Name == "Run");
            foreach (var chart in new FrameworkElement[] { line, pie, gauge })
                Assert.Equal("WPFControls.Charts", chart.GetType().Assembly.GetName().Name);
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
