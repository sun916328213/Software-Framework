using EditorDemo.Services;
using FlowEditor;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using ScriptEditor;
using System.Windows;
using WPFCustomControls;

namespace EditorDemo;

public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<FlowEditorModule>();
        moduleCatalog.AddModule<ScriptEditorModule>();
        moduleCatalog.AddModule<WPFCustomControlsModule>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<DataList>();
    }

    protected override void OnInitialized()
    {
        ContainerManager.Initialize(Container);

      
        base.OnInitialized();
    }
}