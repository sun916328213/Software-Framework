using System.Windows;
using Prism.DryIoc;
using Prism.Ioc;
using WPFControls.Charts.Workbench.ViewModels;
using WPFControls.Charts.Workbench.Views;

namespace WPFControls.Charts.Workbench;

/// <summary>Offline chart host. No production modules, configuration or hardware startup.</summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.Register<MainWindow>();
    }
}
