using Prism.Ioc;
using WPFCustomControls.Window.Views;

namespace WPFCustomControls.Common.Composition;

/// <summary>Registers standalone window views.</summary>
public sealed class WindowDependencyRegistrar : IDependencyRegistrar
{
    public string Name => "Window";

    public void Register(IContainerRegistry registry)
    {
        registry.Register<FileLoading>();
        registry.Register<MessagePrompt>();
        registry.Register<ProgramLoading>();
    }
}
