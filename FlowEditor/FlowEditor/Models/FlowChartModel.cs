using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowEditor.Models;

/// <summary>
/// 流程图运行时模型。
/// 持有缩放、执行状态、日志输出等运行时状态。
/// View 直接绑定到此模型的属性，由 Model 自身触发 PropertyChanged 通知。
/// </summary>
public partial class FlowChartModel : ObservableObject
{
    /// <summary>最小缩放值（20%）。</summary>
    public const double MinZoom = 0.2;
    /// <summary>最大缩放值（200%）。</summary>
    public const double MaxZoom = 2.0;
    /// <summary>每次缩放步进值。</summary>
    public const double ZoomStep = 0.1;
    /// <summary>节点默认宽度（用于自适应计算）。</summary>
    public const double NodeWidth = 200;
    /// <summary>节点默认高度（用于自适应计算）。</summary>
    public const double NodeHeight = 120;
    /// <summary>画布最小宽度。</summary>
    public const double MinCanvasWidth = 3000;
    /// <summary>画布最小高度。</summary>
    public const double MinCanvasHeight = 2000;
    /// <summary>画布边距（节点区域外的缓冲空间）。</summary>
    public const double CanvasPadding = 500;

    /// <summary>当前文件路径（用于保存/另存为区分）。</summary>
    public string? CurrentFilePath { get; set; }

    #region 缩放

    private double _zoomLevel = 1.0;

    /// <summary>当前缩放级别 (0.2 ~ 2.0)。</summary>
    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            value = Math.Clamp(value, MinZoom, MaxZoom);
            if (SetProperty(ref _zoomLevel, value))
            {
                OnPropertyChanged(nameof(ZoomPercentage));
            }
        }
    }

    /// <summary>缩放百分比显示文本（由 ZoomLevel 派生）。</summary>
    public string ZoomPercentage => $"{ZoomLevel * 100:F0}%";

    #endregion

    #region 执行状态

    private bool _isRunning;

    /// <summary>是否正在执行流程。</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    private bool _isDebugMode;

    /// <summary>是否处于单步调试模式。</summary>
    public bool IsDebugMode
    {
        get => _isDebugMode;
        set => SetProperty(ref _isDebugMode, value);
    }

    private bool _isPaused;

    /// <summary>是否暂停（断点等待）。</summary>
    public bool IsPaused
    {
        get => _isPaused;
        set => SetProperty(ref _isPaused, value);
    }

    private bool _isLoopRunning;

    /// <summary>是否正在循环执行。</summary>
    public bool IsLoopRunning
    {
        get => _isLoopRunning;
        set => SetProperty(ref _isLoopRunning, value);
    }

    private int _loopCount = 10;

    /// <summary>循环执行总次数（默认10）。</summary>
    public int LoopCount
    {
        get => _loopCount;
        set => SetProperty(ref _loopCount, Math.Max(1, value));
    }

    private int _loopInterval = 200;

    /// <summary>每次循环之间的间隔（毫秒），默认1000ms。</summary>
    public int LoopInterval
    {
        get => _loopInterval;
        set => SetProperty(ref _loopInterval, Math.Max(0, value));
    }

    private int _currentLoopIndex;

    /// <summary>当前循环次数（从1开始）。</summary>
    public int CurrentLoopIndex
    {
        get => _currentLoopIndex;
        set
        {
            if (SetProperty(ref _currentLoopIndex, value))
                OnPropertyChanged(nameof(LoopProgressText));
        }
    }

    private bool _loopInfinite = true;

    /// <summary>是否无限循环。</summary>
    public bool LoopInfinite
    {
        get => _loopInfinite;
        set
        {
            if (SetProperty(ref _loopInfinite, value))
                OnPropertyChanged(nameof(LoopProgressText));
        }
    }

    /// <summary>循环进度显示文本。</summary>
    public string LoopProgressText =>
        CurrentLoopIndex > 0 ? $"第 {CurrentLoopIndex} 次" : string.Empty;

    #endregion

    #region 日志与状态

    private string _outputText = string.Empty;

    /// <summary>输出面板文本。</summary>
    public string OutputText
    {
        get => _outputText;
        set => SetProperty(ref _outputText, value);
    }

    private string _statusText = "就绪";

    /// <summary>状态栏文本。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    #endregion

    #region 画布尺寸

    private double _canvasWidth = 4000;

    /// <summary>画布宽度（根据节点位置自动调整，不小于 MinCanvasWidth）。</summary>
    public double CanvasWidth
    {
        get => _canvasWidth;
        set => SetProperty(ref _canvasWidth, Math.Max(value, MinCanvasWidth));
    }

    private double _canvasHeight = 3000;

    /// <summary>画布高度（根据节点位置自动调整，不小于 MinCanvasHeight）。</summary>
    public double CanvasHeight
    {
        get => _canvasHeight;
        set => SetProperty(ref _canvasHeight, Math.Max(value, MinCanvasHeight));
    }

    #endregion

    #region 数据连线显示

    private bool _showDataConnections = true;

    /// <summary>是否显示数据连线（非 Flow 类型的数据传输连线）。</summary>
    public bool ShowDataConnections
    {
        get => _showDataConnections;
        set => SetProperty(ref _showDataConnections, value);
    }

    #endregion

    #region 多标签页

    private string _tabTitle = "流程图1";

    /// <summary>标签页标题。</summary>
    public string TabTitle
    {
        get => _tabTitle;
        set
        {
            if (IsRunning || IsLoopRunning || string.IsNullOrWhiteSpace(value)) return;
            if (SetProperty(ref _tabTitle, value))
                OnPropertyChanged(nameof(DisplayTitle));
        }
    }

    private bool _isModified;

    /// <summary>是否有未保存的修改。</summary>
    public bool IsModified
    {
        get => _isModified;
        set
        {
            if (SetProperty(ref _isModified, value))
                OnPropertyChanged(nameof(DisplayTitle));
        }
    }

    /// <summary>显示标题（含修改标记）。</summary>
    public string DisplayTitle => IsModified ? $"{TabTitle} *" : TabTitle;

    #endregion
}
