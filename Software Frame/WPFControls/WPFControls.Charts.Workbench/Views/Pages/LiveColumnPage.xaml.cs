namespace WPFControls.Charts.Workbench.Views.Pages;

public partial class LiveColumnPage : ChartRefreshView
{
    public LiveColumnPage() => InitializeComponent();
    protected override void RefreshCore() => Chart.RefreshChart();
}
