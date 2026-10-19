using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WPFControls.Charts.Workbench.ViewModels;
using WPFControls.Charts.Workbench.Views;
using WPFControls.Charts.Workbench.Views.Pages;
using Xunit;

namespace WPFControls.Tests;

[Collection("Prism module registration")]
public sealed class ChartsWorkbenchTests
{
    [Fact]
    public void PagesHaveIndependentDataCommandsSettingsAndCounters() => OnSta(() =>
    {
        using var shell = new MainWindowViewModel();
        Assert.Equal(6, shell.Pages.Count);
        var line = shell.Pages.Single(page => page.Kind == ChartPageKind.LiveLine);
        var otherLine = shell.Pages.Single(page => page.Kind == ChartPageKind.OxyLine);
        var pie = shell.Pages.Single(page => page.Kind == ChartPageKind.LivePie);
        Assert.NotSame(line.ReplaceDataCommand, otherLine.ReplaceDataCommand);
        Assert.NotSame(line.StartCommand, otherLine.StartCommand);
        Assert.NotSame(line.Lines, otherLine.Lines);
        Assert.NotSame(line.Lines![0], otherLine.Lines![0]);
        Assert.Null(line.PieData);
        Assert.Null(pie.Lines);
        var otherValue = otherLine.Lines[0].Value[0];
        line.PointCount = 1000;
        line.ReplaceDataCommand.Execute(null);
        Assert.All(line.Lines!, item => Assert.Equal(1000, item.Value.Count));
        Assert.Equal(100, otherLine.PointCount);
        Assert.Equal(otherValue, otherLine.Lines[0].Value[0]);
        line.ReportRefresh(1);
        Assert.Equal(1, line.RefreshCount);
        Assert.Equal(0, otherLine.RefreshCount);
        var lineCount = line.Lines!.Count;
        pie.AddCategoryCommand.Execute(null);
        Assert.Equal(4, pie.PieData!.Count);
        Assert.Equal(lineCount, line.Lines.Count);
        pie.ClearDataCommand.Execute(null);
        Assert.Null(pie.PieData);
        Assert.NotEmpty(line.Lines);
    });

    [Fact]
    public void SwitchingStopsPreviousPageAndResetOnlyReplacesCurrentPage() => OnSta(() =>
    {
        using var shell = new MainWindowViewModel();
        var first = shell.SelectedPage!;
        var next = shell.Pages[1];
        first.StartCommand.Execute(null);
        Assert.True(first.IsRunning);
        shell.SelectedPage = next;
        Assert.False(first.IsRunning);
        Assert.False(next.IsRunning);
        next.StartCommand.Execute(null);
        shell.ResetPageCommand.Execute(null);
        Assert.NotSame(next, shell.SelectedPage);
        Assert.Equal(next.Kind, shell.SelectedPage!.Kind);
        Assert.False(shell.SelectedPage.IsRunning);
        Assert.False(next.StartCommand.CanExecute(null));
        Assert.Same(first, shell.Pages[0]);
        shell.Dispose();
        Assert.All(shell.Pages, page => Assert.False(page.StartCommand.CanExecute(null)));
    });

    [Fact]
    public void InPlaceEditsRequireExplicitRefreshAndEmptySeriesCanBeAddedAgain() => OnSta(() =>
    {
        using var model = new ChartPageViewModel(ChartPageKind.LiveLine) { AutoRefresh = false };
        var notifications = 0;
        model.RefreshRequested += () => notifications++;
        var source = model.Lines!;
        var firstValue = source[0].Value[0];
        model.MutateDataCommand.Execute(null);
        Assert.Same(source, model.Lines);
        Assert.Equal(firstValue + 5, source[0].Value[0]);
        Assert.Equal(0, notifications);
        model.RefreshCommand.Execute(null);
        Assert.Equal(1, notifications);
        model.RemoveSeriesCommand.Execute(null);
        model.RemoveSeriesCommand.Execute(null);
        model.AddSeriesCommand.Execute(null);
        Assert.Equal(model.Labels.Count, Assert.Single(model.Lines!).Value.Count);
        for (var index = 0; index < 10; index++) model.AddSeriesCommand.Execute(null);
        Assert.Equal(6, model.Lines!.Count);
        Assert.Equal(1, notifications);
    });

    [Fact]
    public void ButtonsFollowDataRunningAndCapacityStates() => OnSta(() =>
    {
        using var model = new ChartPageViewModel(ChartPageKind.LiveColumn);
        Assert.Equal(12, model.PointCount);
        Assert.True(model.AutoRefresh);
        var refreshed = 0;
        model.RefreshRequested += () => refreshed++;
        model.MutateDataCommand.Execute(null);
        Assert.Equal(1, refreshed);
        model.StartCommand.Execute(null);
        Assert.False(model.ReplaceDataCommand.CanExecute(null));
        Assert.False(model.MutateDataCommand.CanExecute(null));
        Assert.False(model.AddSeriesCommand.CanExecute(null));
        Assert.True(model.StopCommand.CanExecute(null));
        model.StopCommand.Execute(null);
        while (model.AddSeriesCommand.CanExecute(null)) model.AddSeriesCommand.Execute(null);
        Assert.Equal(6, model.Lines!.Count);
        Assert.False(model.AddCategoryCommand.CanExecute(null));
        model.ClearDataCommand.Execute(null);
        Assert.False(model.StartCommand.CanExecute(null));
        Assert.False(model.MutateDataCommand.CanExecute(null));
        Assert.False(model.RemoveSeriesCommand.CanExecute(null));
        Assert.True(model.ReplaceDataCommand.CanExecute(null));
        model.ReplaceDataCommand.Execute(null);
        Assert.True(model.StartCommand.CanExecute(null));
        Assert.DoesNotContain(Enum.GetNames<ChartPageKind>(), name => name == "Comparison");
    });

    [Fact]
    public void StopAndDisposePreventFurtherTimerTicks() => OnSta(() =>
    {
        using var model = new ChartPageViewModel(ChartPageKind.OxyPie) { IntervalMilliseconds = 100 };
        Assert.False(model.IsRunning);
        model.StartCommand.Execute(null);
        Assert.False(model.StartCommand.CanExecute(null));
        PumpUntil(() => model.TickCount > 0);
        model.StopCommand.Execute(null);
        var count = model.TickCount;
        PumpFor(TimeSpan.FromMilliseconds(250));
        Assert.Equal(count, model.TickCount);
        model.StartCommand.Execute(null);
        model.Dispose();
        count = model.TickCount;
        PumpFor(TimeSpan.FromMilliseconds(250));
        Assert.Equal(count, model.TickCount);
        Assert.False(model.StartCommand.CanExecute(null));
        model.StartCommand.Execute(null);
        Assert.False(model.IsRunning);
    });

    [Theory]
    [InlineData(ChartPageKind.LiveLine, typeof(LiveLinePage))]
    [InlineData(ChartPageKind.LivePie, typeof(LivePiePage))]
    [InlineData(ChartPageKind.LiveGauge, typeof(LiveGaugePage))]
    [InlineData(ChartPageKind.OxyLine, typeof(OxyLinePage))]
    [InlineData(ChartPageKind.OxyPie, typeof(OxyPiePage))]
    [InlineData(ChartPageKind.LiveColumn, typeof(LiveColumnPage))]
    public void EachPageLoadsOnlyItsChartAndUnsubscribesWhenUnmounted(ChartPageKind kind, Type expected) => OnSta(() =>
    {
        using var model = new ChartPageViewModel(kind);
        var view = new ChartDemoPageView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 700 };
        try
        {
            window.Show();
            DrainBindings();
            var chart = Assert.Single(Descendants(view).OfType<ChartRefreshView>());
            Assert.Equal(expected, chart.GetType());
            AssertOneRefresh(model);
            model.ChartsMounted = false;
            PumpUntil(() => !chart.IsLoaded);
            var count = model.RefreshCount;
            model.RefreshCommand.Execute(null);
            Assert.Equal(count, model.RefreshCount);
            model.ChartsMounted = true;
            DrainBindings();
            AssertOneRefresh(model);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void LoadedViewDetachesOldContextAndConnectsNewContextOnce() => OnSta(() =>
    {
        using var first = new ChartPageViewModel(ChartPageKind.OxyLine);
        using var second = new ChartPageViewModel(ChartPageKind.OxyLine);
        var view = new OxyLinePage { DataContext = first };
        var window = new Window { Content = view };
        try
        {
            window.Show();
            DrainBindings();
            AssertOneRefresh(first);
            view.DataContext = second;
            DrainBindings();
            var before = first.RefreshCount;
            first.RefreshCommand.Execute(null);
            Assert.Equal(before, first.RefreshCount);
            AssertOneRefresh(second);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void ClosingWindowDisposesEveryPageSession() => OnSta(() =>
    {
        using var shell = new MainWindowViewModel();
        var window = new MainWindow(shell);
        try
        {
            window.Show();
            shell.SelectedPage!.StartCommand.Execute(null);
            window.Close();
            Assert.All(shell.Pages, page =>
            {
                Assert.False(page.IsRunning);
                Assert.False(page.StartCommand.CanExecute(null));
            });
        }
        finally { if (window.IsVisible) window.Close(); }
    });

    [Fact]
    public void WorkbenchDoesNotReferenceProductionHostOrAggregationAssembly()
    {
        var references = typeof(MainWindowViewModel).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, name => name.Name is "WPFControls" or "MachineApp");
        Assert.Contains(references, name => name.Name == "WPFControls.Charts");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void AssertOneRefresh(ChartPageViewModel model)
    {
        var before = model.RefreshCount;
        model.RefreshCommand.Execute(null);
        Assert.Equal(before + 1, model.RefreshCount);
    }

    private static void DrainBindings()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void PumpFor(TimeSpan duration)
    {
        var elapsed = Stopwatch.StartNew();
        PumpUntil(() => elapsed.Elapsed >= duration);
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        EventHandler handler = (_, _) =>
        {
            if (condition() || elapsed.Elapsed > TimeSpan.FromSeconds(5)) frame.Continue = false;
        };
        timer.Tick += handler;
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); timer.Tick -= handler; }
        Assert.True(condition(), "STA dispatcher condition timed out.");
    }

    private static void OnSta(Action action)
    {
        ExceptionDispatchInfo? fault = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { fault = ExceptionDispatchInfo.Capture(error); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Chart workbench STA test timed out.");
        fault?.Throw();
    }
}
