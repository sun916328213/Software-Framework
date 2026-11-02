using System.Windows;
using System.Windows.Controls;

namespace Licensing.Wpf;

/// <summary>非模态提示，不依赖厂商 SDK 或主程序主题。提示本身不作为业务授权守卫。</summary>
internal sealed class LicenseNotificationWindow : Window
{
    private readonly TextBlock _message;

    public LicenseNotificationWindow(Window owner)
    {
        Owner = owner;
        Title = "软件使用期限";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var content = new StackPanel { Margin = new Thickness(20) };
        _message = new TextBlock { TextWrapping = TextWrapping.Wrap };
        content.Children.Add(_message);
        var close = new Button
        {
            Content = "知道了", Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Right,
            IsCancel = true
        };
        close.Click += (_, _) => Close();
        content.Children.Add(close);
        Content = content;
    }

    public void SetMessage(string message) => _message.Text = message;
}
