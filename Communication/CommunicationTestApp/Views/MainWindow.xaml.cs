using System.Windows;
using System.Windows.Media;
using CommunicationTestApp.Services;
using CommunicationTestApp.ViewModels;

namespace CommunicationTestApp.Views;

/// <summary>主窗口。</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 日志滚动跟随：订阅日志集合变化，自动滚动到底部。
        if (_viewModel.Logs is System.Collections.ObjectModel.ObservableCollection<LogEntry> logs)
        {
            logs.CollectionChanged += (_, args) =>
            {
                if (_viewModel.AutoScrollLog && args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    var list = FindLogListBox();
                    list?.ScrollIntoView(logs[^1]);
                }
            };
        }
    }

    private System.Windows.Controls.ListBox? FindLogListBox()
    {
        return FindVisualChildren<System.Windows.Controls.ListBox>(this).LastOrDefault();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
            {
                yield return result;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}

/// <summary>日志级别 → 前景色。</summary>
public sealed class LevelToBrushConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is LogLevel level
            ? level switch
            {
                LogLevel.Success => App.Current.FindResource("Brush.Success"),
                LogLevel.Warning => App.Current.FindResource("Brush.Warning"),
                LogLevel.Error => App.Current.FindResource("Brush.Danger"),
                LogLevel.Rx => App.Current.FindResource("Brush.Info"),
                LogLevel.Tx => App.Current.FindResource("Brush.Warning"),
                _ => App.Current.FindResource("Brush.Fg.Secondary"),
            }
            : App.Current.FindResource("Brush.Fg.Secondary");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>连接状态 → 指示灯颜色。</summary>
public sealed class ConnectedToBrushConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is true
            ? App.Current.FindResource("Brush.Success")
            : App.Current.FindResource("Brush.Fg.Muted");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
