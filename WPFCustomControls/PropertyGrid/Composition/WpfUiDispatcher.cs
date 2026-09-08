using System.Windows;
using System.Windows.Threading;
using WPFCustomControls.PropertyGrid.Composition;

namespace WPFCustomControls.PropertyGrid.Wpf;

/// <summary>WPF adapter for the platform-neutral UI dispatch seam.</summary>
internal sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    private WpfUiDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    public static IUiDispatcher CreateForCurrentApplication() =>
        Application.Current is { } application
            ? new WpfUiDispatcher(application.Dispatcher)
            : NullUiDispatcher.Instance;
}
