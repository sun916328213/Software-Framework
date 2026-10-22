using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WPFControls.Charts.Workbench.ViewModels;

/// <summary>Owns independent demo pages. Navigation stops the previous page's simulation.</summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly ObservableCollection<ChartPageViewModel> pages;
    private bool disposed;

    public MainWindowViewModel()
    {
        pages = new(Enum.GetValues<ChartPageKind>().Select(kind => new ChartPageViewModel(kind)));
        Pages = new(pages);
        SelectedPage = pages[0];
    }

    public ReadOnlyObservableCollection<ChartPageViewModel> Pages { get; }
    [ObservableProperty] private ChartPageViewModel? selectedPage;
    [ObservableProperty] private bool isDarkTheme;

    partial void OnSelectedPageChanging(ChartPageViewModel? oldValue, ChartPageViewModel? newValue)
    {
        dispatcher.VerifyAccess();
        if (oldValue is { IsRunning: true } && !ReferenceEquals(oldValue, newValue))
            oldValue.StopCommand.Execute(null);
    }

    [RelayCommand]
    private void ResetPage()
    {
        dispatcher.VerifyAccess();
        if (disposed || SelectedPage is null) return;
        var old = SelectedPage;
        var index = pages.IndexOf(old);
        if (index < 0) return;
        var replacement = new ChartPageViewModel(old.Kind);
        old.Dispose();
        pages[index] = replacement;
        SelectedPage = replacement;
    }

    public void Dispose()
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        disposed = true;
        foreach (var page in pages) page.Dispose();
    }
}
