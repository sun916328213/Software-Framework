namespace WPFControls.Charts.Workbench.Views;

/// <summary>Optional comparison page; other chart types have separate pages and sessions.</summary>
public partial class ChartComparisonView : ChartRefreshView
{
    public ChartComparisonView() => InitializeComponent();

    protected override void RefreshCore()
    {
        LiveLine.RefreshChart();
        LivePie.RefreshChart();
        LiveGauge.RefreshChart();
        OxyLine.RefreshChart();
        OxyPie.RefreshChart();
    }
}
