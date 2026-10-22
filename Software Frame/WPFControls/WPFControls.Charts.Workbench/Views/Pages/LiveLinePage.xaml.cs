namespace WPFControls.Charts.Workbench.Views.Pages;

public partial class LiveLinePage : ChartRefreshView
{
    public LiveLinePage() => InitializeComponent();
    protected override void RefreshCore() => Chart.RefreshChart();
}
