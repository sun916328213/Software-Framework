using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WPFControls.Charts.Workbench.ViewModels;

namespace WPFControls.Charts.Workbench.Views;

/// <summary>Shared view-only subscription lifecycle. Each page keeps its own commands and session.</summary>
public class ChartRefreshView : UserControl
{
    private ChartPageViewModel? subscribedContext;

    public ChartRefreshView()
    {
        Loaded += OnLoaded;
        Unloaded += (_, _) => Attach(null);
        DataContextChanged += OnContextChanged;
    }

    protected virtual void RefreshCore() { }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        Attach(DataContext as ChartPageViewModel);
        RefreshCharts();
    }

    private void OnContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (!IsLoaded) return;
        Attach(args.NewValue as ChartPageViewModel);
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (IsLoaded && ReferenceEquals(DataContext, args.NewValue)) RefreshCharts();
        }));
    }

    private void Attach(ChartPageViewModel? context)
    {
        if (ReferenceEquals(subscribedContext, context)) return;
        if (subscribedContext is not null) subscribedContext.RefreshRequested -= RefreshCharts;
        subscribedContext = context;
        if (context is not null) context.RefreshRequested += RefreshCharts;
    }

    private void RefreshCharts()
    {
        VerifyAccess();
        if (subscribedContext is null) return;
        var start = Stopwatch.GetTimestamp();
        RefreshCore();
        subscribedContext.ReportRefresh(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
