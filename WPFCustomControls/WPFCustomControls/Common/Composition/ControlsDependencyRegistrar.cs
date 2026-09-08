using System.Reflection;
using Prism.Ioc;
using System.Windows.Controls;

namespace WPFCustomControls.Common.Composition;

/// <summary>Registers reusable control views.</summary>
public sealed class ControlsDependencyRegistrar : IDependencyRegistrar
{
    public string Name => "Controls";

    public void Register(IContainerRegistry registry)
    {
        var viewTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && type.Namespace?.StartsWith("WPFCustomControls.Controls", StringComparison.Ordinal) == true
                           && type.IsSubclassOf(typeof(UserControl)));

        foreach (var viewType in viewTypes)
        {
            registry.RegisterSingleton(viewType);
            registry.RegisterSingleton(typeof(object), viewType, viewType.Name);
        }
    }
}
