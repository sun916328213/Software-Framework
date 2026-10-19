using Extension.Core.Reflection;
using WPFControls.Charts.Models;
using WPFControls.Controls.Diagnostics;
using WPFControls.Controls.Services.Charts;
using WPFControls.Controls.Views.Charts;
using Xunit;

namespace WPFControls.Tests;

public sealed class ChartsExtractionTests
{
    public static IEnumerable<object[]> ChartTypes()
    {
        foreach (var type in new[]
        {
            typeof(ChartLineData), typeof(ChartPieData),
            typeof(ChartsLineService), typeof(ChartsLineType), typeof(ChartsPieService), typeof(ChartsPieType),
            typeof(ChartsLineView), typeof(ChartsPieView), typeof(ChartsMultipleGaugeView)
        })
            yield return new object[] { type };
    }

    [Theory]
    [MemberData(nameof(ChartTypes))]
    public void ChartTypesBelongToIndependentAssembly(Type type)
    {
        Assert.Equal("WPFControls.Charts", type.Assembly.GetName().Name);
        Assert.DoesNotContain(type.Assembly.GetReferencedAssemblies(), name => name.Name == "WPFControls");
        Assert.Null(typeof(WPFControlsModule).Assembly.GetType(type.FullName!));
    }

    [Theory]
    [MemberData(nameof(ChartTypes))]
    public void PersistedChartIdentitiesResolveToExtractedTypes(Type type)
    {
        var current = type.FullName!;
        foreach (var identity in new[]
        {
            $"{current}, WPFControls",
            $"{current}, WPFControls.Charts",
            $"{current.Replace("WPFControls.Controls.", "WPFControl.CustomControl.")}, WPFControl",
            $"{current.Replace("WPFControls.Controls.", "WPFControl.Controls.")}, WPFControl.Controls"
        })
            Assert.Same(type, FrameworkTypeResolver.ResolvePersistedType(identity));
    }

    [Theory]
    [InlineData("WPFControls.Controls.Models.Charts.LineSeries, WPFControls.Charts")]
    [InlineData("WPFControl.CustomControl.Models.Charts.LineSeries, WPFControl")]
    [InlineData("WPFControl.Controls.Models.Charts.LineSeries, WPFControl.Controls")]
    public void LegacyLineSeriesIdentityResolvesToSharedModel(string identity)
        => Assert.Same(typeof(ChartLineData), FrameworkTypeResolver.ResolvePersistedType(identity));

    [Theory]
    [InlineData("WPFControls.Controls.Models.Charts.PieData, WPFControls.Charts")]
    [InlineData("WPFControl.CustomControl.Models.Charts.PieData, WPFControl")]
    [InlineData("WPFControl.Controls.Models.Charts.PieData, WPFControl.Controls")]
    public void LegacyPieDataIdentityResolvesToSharedModel(string identity)
        => Assert.Same(typeof(ChartPieData), FrameworkTypeResolver.ResolvePersistedType(identity));

    [Fact]
    public void RemovedBackendModelsLeaveNoDanglingIdentity()
    {
        var assembly = typeof(ChartsLineService).Assembly;
        Assert.Null(assembly.GetType("WPFControls.Controls.Models.Charts.ChartsLineModel"));
        Assert.Null(assembly.GetType("WPFControls.Controls.Models.Charts.ChartsPieModel"));
        Assert.Null(FrameworkTypeResolver.ResolvePersistedType(
            "WPFControls.Controls.Models.Charts.ChartsLineModel, WPFControls.Charts"));
        Assert.Null(FrameworkTypeResolver.ResolvePersistedType(
            "WPFControls.Controls.Models.Charts.ChartsPieModel, WPFControls.Charts"));
    }

    [Fact]
    public void LineServicePreservesSeriesDataAndAxisSettings()
    {
        var service = new ChartsLineService();
        var data = new ChartLineData { SeriesName = "Output", Value = new() { 1, 3, 2 } };
        var series = Assert.IsType<LiveChartsCore.SkiaSharpView.LineSeries<double>>(
            Assert.Single(service.ShowData(new[] { data })));
        Assert.Equal("Output", series.Name);
        Assert.Same(data.Value, series.Values);

        service.SetAxisX(new() { "A", "B", "C" });
        service.SetAxisFontSize(20);
        var axis = Assert.IsType<LiveChartsCore.SkiaSharpView.Axis>(Assert.Single(service.GetAxisX()));
        Assert.Equal(new[] { "A", "B", "C" }, axis.Labels);
        Assert.Equal(20d, axis.TextSize);
        service.Clear();
        Assert.Empty(service.GetSeries());
        Assert.Empty(axis.Labels!);
    }

    [Fact]
    public void NullAndEmptyInputClearSeriesInsteadOfThrowing()
    {
        var line = new ChartsLineService();
        line.ShowData(new[] { new ChartLineData { SeriesName = "Output", Value = new() { 1 } } });
        Assert.Empty(line.ShowData(null!));
        Assert.Empty(line.ShowData(Array.Empty<ChartLineData>()));

        var pie = new ChartsPieService();
        pie.ShowData(new[] { new ChartPieData { SeriesName = "Pass", Value = 3 } });
        Assert.Empty(pie.ShowData(null!));
        Assert.Empty(pie.ShowData(new[] { new ChartPieData { Value = double.NaN } }));
    }

    [Fact]
    public void HostErrorExecutorCanBeAdaptedWithoutReverseReference()
    {
        Exception? reported = null;
        var errors = new TryCatchHelper(error => reported = error);
        var service = new ChartsLineService(action => errors.TryExecute(action));
        service.AddSeries(null!);
        Assert.IsType<NullReferenceException>(reported);
        Assert.Empty(service.GetSeries());
    }
}
