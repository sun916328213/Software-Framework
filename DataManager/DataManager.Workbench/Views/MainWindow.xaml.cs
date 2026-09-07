using DataManager.Workbench.ViewModels;
using System.Windows;

namespace DataManager.Workbench.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => (DataContext as MainWindowViewModel)?.SelectFirst();
        }
    }
}
