using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WPFCustomControls.PropertyGrid.Views;

/// <summary>同一行路径与选文件按钮；只回写绑定值，不读取图像或拥有工具会话。</summary>
public partial class FilePathEditor : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
        typeof(FilePathEditor), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(string),
        typeof(FilePathEditor), new PropertyMetadata("所有文件|*.*"));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool),
        typeof(FilePathEditor), new PropertyMetadata(false, (d, _) => ((FilePathEditor)d).RefreshAvailability()));
    private static readonly DependencyPropertyKey SelectionErrorPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SelectionError), typeof(string), typeof(FilePathEditor), new PropertyMetadata(""));
    public static readonly DependencyProperty SelectionErrorProperty = SelectionErrorPropertyKey.DependencyProperty;
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Filter { get => (string)GetValue(FilterProperty); set => SetValue(FilterProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public string SelectionError => (string)GetValue(SelectionErrorProperty);
    /// <summary>宿主或离线测试可借用选文件委托；未提供时使用当前Owner的系统对话框。</summary>
    public Func<string, string, string?>? PickFile { get; set; }
    private bool _picking;
    public FilePathEditor() => InitializeComponent();
    private void RefreshAvailability()
    {
        if (BrowseButton != null) BrowseButton.IsEnabled = !IsReadOnly && !_picking;
    }
    private void Browse(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || !IsEnabled || IsReadOnly || _picking) return;
        var binding = GetBindingExpression(TextProperty);
        var target = binding?.DataItem;
        _picking = true; RefreshAvailability(); SetValue(SelectionErrorPropertyKey, "");
        try
        {
            var selected = PickFile != null ? PickFile(Filter, Text) : ShowDialog(Filter, Text);
            // 模态对话框期间仍可能被程序化切节点/关闭/启动任务；不得写入已卸载或新绑定的目标。
            if (string.IsNullOrWhiteSpace(selected) || !IsLoaded || !IsEnabled || IsReadOnly ||
                !ReferenceEquals(binding, GetBindingExpression(TextProperty)) ||
                !ReferenceEquals(target, GetBindingExpression(TextProperty)?.DataItem) || selected == Text) return;
            SetCurrentValue(TextProperty, selected);
        }
        catch (Exception error) { SetValue(SelectionErrorPropertyKey, $"选择文件失败：{error.Message}"); }
        finally { _picking = false; RefreshAvailability(); }
    }
    private string? ShowDialog(string filter, string current)
    {
        var dialog = new OpenFileDialog { Title = "选择图像/文件", Filter = filter, CheckFileExists = true, Multiselect = false };
        if (System.IO.File.Exists(current)) dialog.FileName = current;
        var owner = System.Windows.Window.GetWindow(this);
        return (owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true ? dialog.FileName : null;
    }
}
