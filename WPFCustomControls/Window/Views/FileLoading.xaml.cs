using System.Windows;

namespace WPFCustomControls.Window.Views;

public partial class FileLoading : System.Windows.Window
{
    public static readonly DependencyProperty TextProperty;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FileLoading() => InitializeComponent();

    static FileLoading()
    {
        TextProperty = DependencyProperty.Register("Text", typeof(string),
            typeof(FileLoading), new FrameworkPropertyMetadata("Loading....."));
    }
}