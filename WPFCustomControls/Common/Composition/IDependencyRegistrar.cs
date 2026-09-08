using Prism.Ioc;

namespace WPFCustomControls.Common.Composition;

/// <summary>Registers one feature area with the application container.</summary>
public interface IDependencyRegistrar
{
    string Name { get; }

    void Register(IContainerRegistry registry);
}
