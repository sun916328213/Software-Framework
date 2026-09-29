using System.Runtime.CompilerServices;
using System.Windows.Threading;
using ScriptEditor.Models;
using WPFCustomControls.Window.Views;

namespace ScriptEditor.Services;

internal static class ScriptLoadingWindowRegistry
{
    private static readonly ConditionalWeakTable<ScriptItem, FileLoading> Windows = new();

    public static void Set(ScriptItem item, FileLoading? window)
    {
        Windows.Remove(item);
        if (window != null) Windows.Add(item, window);
    }

    public static void Close(ScriptItem item)
    {
        if (!Windows.TryGetValue(item, out var window)) return;
        Windows.Remove(item);
        window.Dispatcher.BeginInvoke(() =>
        {
            window.Close();
            window.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        });
    }
}
