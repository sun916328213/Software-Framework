using System.Windows;
using System.Windows.Input;

namespace WPFCustomControls.Window.Views;

/// <summary>
/// 名称输入弹窗（Window 形式，无边框）。
/// 直接通过属性设置标题/描述/占位符，无需 ViewModel 中间层。
/// </summary>
public partial class NameInputControl : System.Windows.Window
{
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(NameInputControl), new PropertyMetadata("请输入名称："));
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(NameInputControl), new PropertyMetadata("名称"));
    public static readonly DependencyProperty InputTextProperty =
        DependencyProperty.Register(nameof(InputText), typeof(string), typeof(NameInputControl), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string InputText { get => (string)GetValue(InputTextProperty); set => SetValue(InputTextProperty, value); }

    /// <summary>弹出名称输入框，返回输入文本或 null（取消）。</summary>
    public static string? Prompt(string title, string description, string placeholder = "名称")
    {
        var dlg = new NameInputControl
        {
            Title = title,
            Description = description,
            Placeholder = placeholder
        };
        return dlg.ShowDialog() == true ? dlg.InputText : null;
    }

    public NameInputControl()
    {
        InitializeComponent();
        Title = "输入名称";
        DataContext = this;
        Loaded += (_, _) => InputBox.Focus();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void InputBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            DialogResult = true;
    }
}
