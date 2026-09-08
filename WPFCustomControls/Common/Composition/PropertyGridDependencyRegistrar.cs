using Prism.Ioc;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.Common.Composition;

/// <summary>Registers PropertyGrid Core interfaces and their WPF adapters.</summary>
public sealed class PropertyGridDependencyRegistrar : IDependencyRegistrar
{
    public string Name => "PropertyGrid";

    public void Register(IContainerRegistry registry)
    {
        registry.RegisterSingleton<IPropertyGridBuilderService, PropertyGridBuilderService>();
        registry.RegisterSingleton<IPortDialogService, PortDialogService>();
        registry.Register<PropertyGridHostViewModel>();
    }
}
