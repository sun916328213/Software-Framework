using Communication.Workbench.ViewModels;
using System.Windows;

namespace Communication.Workbench.Views;

/// <summary>壳窗口：侧栏导航 + Prism 区域，DataContext 由 Prism 约定自动装配。</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as MainWindowViewModel)?.SelectFirst();
    }
}
