using DataManager.Log;
using Prism.DryIoc;
using Prism.Ioc;
using Secs.Variable;
using Secs.Variable.Data;
using SecsGem.Runtime;
using System.Windows;

namespace SecsGem.Workbench;

public partial class App : PrismApplication
{
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry registry)
    {
        registry.RegisterSingleton<LogManager>();
        registry.RegisterSingleton<ReportID>();
        registry.RegisterSingleton<CEID>();
        registry.RegisterSingleton<DVID>();
        registry.RegisterSingleton<SVID>();
        registry.RegisterSingleton<ECID>();
        registry.RegisterSingleton<Alarm>();
        registry.RegisterSingleton<ActiveReceive>();
        new SecsGemModule().RegisterTypes(registry);
    }
}
