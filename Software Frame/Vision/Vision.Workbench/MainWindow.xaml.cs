using System.Windows;
using Vision.Workbench.ViewModels;

namespace Vision.Workbench;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}
