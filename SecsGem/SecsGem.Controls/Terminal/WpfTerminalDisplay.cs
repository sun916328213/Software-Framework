using Secs;
using System.Windows;

namespace SecsGem.Controls.Terminal;

/// <summary>通过现有 WPF 提示窗口显示 S10 终端消息。</summary>
public sealed class WpfTerminalDisplay : ITerminalDisplay
{
    public void Show(string title, string message)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("S10 terminal display requires a WPF application.");
        dispatcher.Invoke(() => new MessagePrompt(title, message).ShowDialog());
    }
}
