using Extension.UI.Controls;
namespace Secs.Order.S10.Presentation;

/// <summary>通过现有 WPF 提示窗口显示 S10 终端消息。</summary>
public sealed class WpfTerminalDisplay : ITerminalDisplay
{
    public void Show(string title, string message)
    {
        Action show = () => new MessagePrompt(title, message).ShowDialog();
        show.UIInvoke();
    }
}
