namespace WPFControls.Charts.Workbench.Views.Pages;

public partial class OxyPiePage : ChartRefreshView
{
    public OxyPiePage() => InitializeComponent();
    protected override void RefreshCore() => Chart.RefreshChart();
}
