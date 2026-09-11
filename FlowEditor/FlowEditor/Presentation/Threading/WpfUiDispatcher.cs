using System.Windows;
using FlowEditor.Interfaces;

namespace FlowEditor.Presentation.Threading;

/// <summary>使用 WPF Dispatcher 封送界面更新的生产环境 adapter。</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null ||
            dispatcher.HasShutdownStarted ||
            dispatcher.HasShutdownFinished ||
            dispatcher.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            dispatcher.Invoke(action);
        }
        catch (TaskCanceledException)
        {
            action();
        }
        catch (InvalidOperationException) when (
            dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            action();
        }
    }
}
