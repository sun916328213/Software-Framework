using System.ComponentModel;
using System.Windows;
using MaterialDesignThemes.Wpf;
using WPFControls.Charts.Workbench.ViewModels;

namespace WPFControls.Charts.Workbench.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnClosed;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(MainWindowViewModel.IsDarkTheme)) return;
        // Only theme the host, without patching or hiding backend theme defects.
        var theme = Application.Current?.Resources.MergedDictionaries.OfType<BundledTheme>().FirstOrDefault();
        if (theme is not null) theme.BaseTheme = viewModel.IsDarkTheme ? BaseTheme.Dark : BaseTheme.Light;
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Dispose();
        Closed -= OnClosed;
    }
}
