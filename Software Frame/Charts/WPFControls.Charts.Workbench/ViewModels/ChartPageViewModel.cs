using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFControls.Charts.Models;

namespace WPFControls.Charts.Workbench.ViewModels;

public enum ChartPageKind { LiveLine, LiveColumn, LivePie, LiveGauge, OxyLine, OxyPie }

/// <summary>Independent page state, commands and bounded UI-thread-only simulation.</summary>
public partial class ChartPageViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer timer;
    private readonly Stopwatch session = new();
    private bool disposed;
    private double phase;
    private int pointCount;
    private int intervalMilliseconds = 1000;

    public ChartPageViewModel(ChartPageKind kind = ChartPageKind.LiveLine)
    {
        Kind = kind;
        pointCount = kind == ChartPageKind.LiveColumn ? 12 : 100;
        timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        { Interval = TimeSpan.FromMilliseconds(intervalMilliseconds) };
        timer.Tick += OnTick;
        ReplaceData();
    }

    public ChartPageKind Kind { get; }
    public string Title => Kind switch
    {
        ChartPageKind.LiveLine => "LiveCharts · 折线图",
        ChartPageKind.LiveColumn => "LiveCharts · 柱状图",
        ChartPageKind.LivePie => "LiveCharts · 圆环图",
        ChartPageKind.LiveGauge => "LiveCharts · 多仪表盘",
        ChartPageKind.OxyLine => "OxyPlot · 折线图",
        ChartPageKind.OxyPie => "OxyPlot · 饼图 / 圆环图",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
    public bool HasCartesian => Kind is ChartPageKind.LiveLine or ChartPageKind.LiveColumn or ChartPageKind.OxyLine;
    public bool HasCategories => !HasCartesian;
    public bool HasAnimation => Kind is not (ChartPageKind.OxyLine or ChartPageKind.OxyPie);
    public bool HasInnerDiameter => Kind == ChartPageKind.OxyPie;
    public string AddSeriesCaption => Kind == ChartPageKind.LiveColumn ? "添加柱数据组" : "添加曲线";
    public string RemoveSeriesCaption => Kind == ChartPageKind.LiveColumn ? "删除末组柱数据" : "删除末条曲线";
    public string AddCategoryCaption => Kind == ChartPageKind.LiveGauge ? "添加仪表项" : "添加分类";
    public string RemoveCategoryCaption => Kind == ChartPageKind.LiveGauge ? "删除末个仪表项" : "删除末个分类";
    public string DataSizeCaption => Kind == ChartPageKind.LiveColumn ? "类别数量" : "每条曲线点数";
    public IReadOnlyList<int> PointCountOptions => Kind == ChartPageKind.LiveColumn
        ? new[] { 6, 12, 24, 50 } : new[] { 100, 1000, 5000, 10000 };
    public IReadOnlyList<int> IntervalOptions { get; } = new[] { 250, 500, 1000, 2000 };
    public bool HasData => HasCartesian ? Lines is { Count: > 0 } : PieData is { Count: > 0 };
    public bool CanConfigureData => CanEdit();
    public event Action? RefreshRequested;

    [ObservableProperty] private ObservableCollection<ChartLineData>? lines;
    [ObservableProperty] private ObservableCollection<ChartPieData>? pieData;
    [ObservableProperty] private ObservableCollection<string> labels = new();
    [ObservableProperty] private bool chartsMounted = true;
    [ObservableProperty] private bool autoRefresh = true;
    [ObservableProperty] private double axisFontSize = 14;
    [ObservableProperty] private double innerDiameter = 0.6;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationDuration))]
    private bool enableAnimations;
    public TimeSpan AnimationDuration => TimeSpan.FromMilliseconds(EnableAnimations ? 500 : 0);
    [ObservableProperty] private string status = "仅模拟数据。";
    [ObservableProperty] private long tickCount;
    [ObservableProperty] private long refreshCount;
    [ObservableProperty] private double lastRefreshMilliseconds;
    [ObservableProperty] private double managedMemoryMb;
    [ObservableProperty] private double sessionSeconds;
    [ObservableProperty] private bool isRunning;

    partial void OnLinesChanged(ObservableCollection<ChartLineData>? value) => NotifyCommands();
    partial void OnPieDataChanged(ObservableCollection<ChartPieData>? value) => NotifyCommands();
    partial void OnIsRunningChanged(bool value) => NotifyCommands();
    partial void OnChartsMountedChanged(bool value) => NotifyCommands();

    public int PointCount
    {
        get => pointCount;
        set
        {
            dispatcher.VerifyAccess();
            if (value < 1 || value > 10000) throw new ArgumentOutOfRangeException(nameof(value));
            SetProperty(ref pointCount, value);
        }
    }
    public int IntervalMilliseconds
    {
        get => intervalMilliseconds;
        set
        {
            dispatcher.VerifyAccess();
            if (value < 100 || value > 10000) throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref intervalMilliseconds, value)) timer.Interval = TimeSpan.FromMilliseconds(value);
        }
    }

    private bool CanEdit() => !disposed && !IsRunning;
    private bool CanUpdate() => CanEdit() && HasData;
    private bool CanAddSeries() => CanEdit() && HasCartesian && Lines is { Count: < 6 };
    private bool CanRemoveSeries() => CanEdit() && HasCartesian && Lines is { Count: > 0 };
    private bool CanAddCategory() => CanEdit() && HasCategories && PieData is { Count: < 6 };
    private bool CanRemoveCategory() => CanEdit() && HasCategories && PieData is { Count: > 0 };
    private bool CanStart() => CanEdit() && HasData;
    private bool CanStop() => !disposed && IsRunning;
    private bool CanClear() => !disposed && (HasData || IsRunning);
    private bool CanRefresh() => !disposed && ChartsMounted;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ReplaceData()
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        phase += 0.3;
        if (HasCartesian)
        {
            var nextLabels = new ObservableCollection<string>();
            var first = new ChartLineData { SeriesName = "工位 A", Color = Brushes.Teal };
            var second = new ChartLineData { SeriesName = "工位 B", Color = Brushes.DarkOrange };
            var stride = Math.Max(1, PointCount / 10);
            for (var index = 0; index < PointCount; index++)
            {
                var x = index * 4 * Math.PI / Math.Max(1, PointCount - 1);
                first.Value.Add(50 + 25 * Math.Sin(x + phase));
                second.Value.Add(45 + 15 * Math.Cos(x + phase));
                nextLabels.Add(Kind == ChartPageKind.LiveColumn ? $"类别 {index + 1}"
                    : index % stride == 0 ? $"点 {index}" : string.Empty);
            }
            Labels = nextLabels;
            Lines = new() { first, second };
        }
        else
            PieData = new()
            {
                new() { SeriesName = "运行", Value = 72 + 3 * Math.Sin(phase), Color = Brushes.Teal },
                new() { SeriesName = "待机", Value = 20 - 3 * Math.Sin(phase), Color = Brushes.DarkOrange },
                new() { SeriesName = "报警", Value = 8, Color = Brushes.IndianRed }
            };
        Status = "已生成本页数据。";
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private void MutateData()
    {
        dispatcher.VerifyAccess();
        if (!CanUpdate()) return;
        if (Lines is not null)
            foreach (var line in Lines)
                if (line.Value.Count > 0) line.Value[0] += 5;
        if (PieData is { Count: > 0 }) PieData[0].Value = Math.Min(100, PieData[0].Value + 1);
        AfterEdit("已更新首个数值");
    }

    [RelayCommand(CanExecute = nameof(CanAddSeries))]
    private void AddSeries()
    {
        dispatcher.VerifyAccess();
        if (!CanAddSeries()) return;
        Lines!.Add(new ChartLineData
        {
            SeriesName = $"数据组 {Lines.Count + 1}", Color = Brushes.SlateBlue,
            Value = new ObservableCollection<double>(Enumerable.Repeat(30d + Lines.Count * 5,
                Lines.FirstOrDefault()?.Value.Count ?? Labels.Count))
        });
        AfterEdit("已添加数据组");
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSeries))]
    private void RemoveSeries()
    {
        dispatcher.VerifyAccess();
        if (!CanRemoveSeries()) return;
        Lines!.RemoveAt(Lines.Count - 1);
        AfterEdit("已删除末个数据组");
    }

    [RelayCommand(CanExecute = nameof(CanAddCategory))]
    private void AddCategory()
    {
        dispatcher.VerifyAccess();
        if (!CanAddCategory()) return;
        PieData!.Add(new ChartPieData { SeriesName = $"分类 {PieData.Count + 1}", Value = 10, Color = Brushes.SlateBlue });
        AfterEdit("已添加分类 / 仪表项");
    }

    [RelayCommand(CanExecute = nameof(CanRemoveCategory))]
    private void RemoveCategory()
    {
        dispatcher.VerifyAccess();
        if (!CanRemoveCategory()) return;
        PieData!.RemoveAt(PieData.Count - 1);
        AfterEdit("已删除末个分类 / 仪表项");
    }

    private void AfterEdit(string message)
    {
        NotifyCommands();
        Status = message + (AutoRefresh ? "，已刷新。" : "；诊断模式：未显式刷新。");
        if (AutoRefresh) Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void ClearData()
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        Stop();
        Lines = null;
        PieData = null;
        Labels = new();
        Status = "本页已清空，模拟已停止；生成数据后可重新开始。";
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private void Refresh()
    {
        dispatcher.VerifyAccess();
        if (CanRefresh()) RefreshRequested?.Invoke();
    }

    public void ReportRefresh(double elapsedMilliseconds)
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        LastRefreshMilliseconds = elapsedMilliseconds;
        RefreshCount++;
        ManagedMemoryMb = GC.GetTotalMemory(false) / 1024d / 1024;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        dispatcher.VerifyAccess();
        if (!CanStart()) return;
        TickCount = 0;
        session.Restart();
        SessionSeconds = 0;
        IsRunning = true;
        timer.Start();
        Status = "本页模拟运行中；停止后可手动编辑，切页会停止模拟。";
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        dispatcher.VerifyAccess();
        timer.Stop();
        session.Stop();
        SessionSeconds = session.Elapsed.TotalSeconds;
        IsRunning = false;
        if (!disposed) Status = "本页模拟已停止。";
    }

    private void OnTick(object? sender, EventArgs args)
    {
        if (disposed || !IsRunning) return;
        try
        {
            TickCount++;
            SessionSeconds = session.Elapsed.TotalSeconds;
            ReplaceData();
        }
        catch (Exception error)
        {
            Stop();
            Status = $"本页模拟失败，已停止：{error.Message}";
            Trace.TraceError(error.ToString());
        }
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(CanConfigureData));
        ReplaceDataCommand.NotifyCanExecuteChanged();
        MutateDataCommand.NotifyCanExecuteChanged();
        AddSeriesCommand.NotifyCanExecuteChanged();
        RemoveSeriesCommand.NotifyCanExecuteChanged();
        AddCategoryCommand.NotifyCanExecuteChanged();
        RemoveCategoryCommand.NotifyCanExecuteChanged();
        ClearDataCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        Stop();
        disposed = true;
        timer.Tick -= OnTick;
        RefreshRequested = null;
        NotifyCommands();
    }
}
