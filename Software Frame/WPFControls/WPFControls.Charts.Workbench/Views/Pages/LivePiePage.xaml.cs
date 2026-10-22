namespace WPFControls.Charts.Workbench.Views.Pages;

public partial class LivePiePage : ChartRefreshView
{
    public LivePiePage() => InitializeComponent();
    protected override void RefreshCore() => Chart.RefreshChart();
}
