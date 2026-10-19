namespace WPFControls.Charts.Workbench.Views.Pages;

public partial class LiveGaugePage : ChartRefreshView
{
    public LiveGaugePage() => InitializeComponent();
    protected override void RefreshCore() => Chart.RefreshChart();
}
