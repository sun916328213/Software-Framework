using Extension.UI.Theme;
using Prism.Regions;
using Vision.Controls.View;
using WPFCustomControls.Workbench;

namespace Vision.Workbench.ViewModels;

/// <summary>只导航Controls中的视觉流程页，不声明工具、节点参数或执行逻辑。</summary>
public sealed class MainWindowViewModel : WorkbenchNavigationViewModel<WorkbenchPage>
{
    public const string MainRegion = "VisionRegion";
    private readonly IRegionManager _regions;
    public MainWindowViewModel(IRegionManager regions, ThemeService theme)
        : base(regions, theme, MainRegion,
            [new("视觉流程", nameof(VisionFlowView))], [new("视觉流程", nameof(VisionFlowView))])
    {
        _regions = regions;
    }
    protected override void OnNavigationSucceeded(WorkbenchPage page)
    {
        if (_regions.Regions[MainRegion].ActiveViews.OfType<VisionFlowView>().FirstOrDefault() is { } flow)
            flow.ToolbarAccessory = System.Windows.Application.Current.MainWindow.FindName("Appearance");
    }
}
