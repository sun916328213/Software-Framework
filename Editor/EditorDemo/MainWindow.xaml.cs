using FlowEditor.Views;
using Prism.Ioc;
using Prism.Regions;
using System.Windows;
using ScriptEditor.Services;
using ScriptEditor.Views;

namespace EditorDemo;

public partial class MainWindow : Window
{
    private readonly IRegionManager _regionManager;
    private readonly ScriptManagerService _scriptService;

    public MainWindow(IContainerProvider container, IRegionManager regionManager)
    {
        _regionManager = regionManager;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        _regionManager.RequestNavigate("ScriptRegion", nameof(ScriptManagerView));
    
        _regionManager.RequestNavigate("FlowRegion", nameof(FlowChartContainerView));
    }
}
