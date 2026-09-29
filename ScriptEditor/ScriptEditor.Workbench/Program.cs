using System.Windows;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Mvvm;
using ScriptEditor.Views;

namespace ScriptEditor.Workbench;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new Application();
        var container = new DryIocContainerExtension(new Container());
        ContainerLocator.SetContainerExtension(() => container);
        ViewModelLocationProvider.SetDefaultViewModelFactory(type => container.Resolve(type));

        var module = new ScriptEditorModule();
        module.RegisterTypes(container);
        module.OnInitialized(container);

        var window = new Window
        {
            Title = "ScriptEditor Workbench",
            Width = 1200,
            Height = 800,
            MinWidth = 640,
            MinHeight = 400,
            Content = container.Resolve<ScriptManagerView>()
        };
        ScriptEditorView.SetEditorService(window, container.Resolve<ScriptEditor.Services.ScriptEditorService>());
        if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
            window.Loaded += (_, _) => window.Close();
        app.Run(window);
    }
}
