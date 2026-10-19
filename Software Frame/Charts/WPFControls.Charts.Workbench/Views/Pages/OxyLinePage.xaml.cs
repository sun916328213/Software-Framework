namespace WPFControls.Charts.Workbench.Views.Pages;

public partial class OxyLinePage : ChartRefreshView
{
    public OxyLinePage() => InitializeComponent();
    protected override void RefreshCore() => Chart.RefreshChart();
}
