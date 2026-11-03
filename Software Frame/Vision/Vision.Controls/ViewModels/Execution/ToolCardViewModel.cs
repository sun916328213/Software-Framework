// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolCardViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System.ComponentModel、Vision.Controls.Models、Vision.Controls.Services、Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Core.Imaging;
#nullable enable
namespace Vision.Controls.ViewModels;
/// <summary>工具卡片绑定和 Toolkit 命令；执行与资源释放由 Workspace 负责。</summary>
public sealed partial class ToolCardViewModel : ObservableObject, IDisposable
{
    private readonly ToolCardWorkspace _workspace;
    private bool _disposed;
    private bool _closing;
    private Task? _activeRun;
    private bool CanUseCard() => !_disposed && !_closing && !Model.IsBusy;
    private bool CanExecuteCommand() => CanUseCard() && !Model.IsReadOnly;
    public IAsyncRelayCommand RunCommand { get; }
    public bool SupportsSearchRegion => _workspace.SupportsSearchRegion;
    internal void SetExecutionSearchFixture(Vision.Core.Geometry.Pose2D reference, Vision.Core.Geometry.Pose2D? current) => _workspace.ExecutionSearchFixture = (reference, current);
    public IReadOnlyList<Vision.Core.Results.NumericParameterDefinition> NumericInputs => _workspace.NumericInputs;
    internal void SetExecutionNumericParameter(Vision.Core.Results.NumericParameterValue? value) => _workspace.ExecutionNumericParameter = value;
    public IReadOnlyList<Vision.Core.Results.LineParameterDefinition> LineInputs => _workspace.LineInputs;
    internal void SetExecutionLineParameter(Vision.Core.Results.LineParameterValue? value) => _workspace.ExecutionLineParameter = value;
    public IReadOnlyList<Vision.Core.Results.CircleParameterDefinition> CircleInputs => _workspace.CircleInputs;
    internal void SetExecutionCircleParameter(Vision.Core.Results.CircleParameterValue? value) => _workspace.ExecutionCircleParameter = value;
    public IReadOnlyList<Vision.Core.Results.PointParameterDefinition> PointInputs => _workspace.PointInputs;
    internal void SetExecutionPointParameter(Vision.Core.Results.PointParameterValue? value) => _workspace.ExecutionPointParameter = value;
    public ToolCardModel Model => _workspace.Model;
    public ResultSelectionViewModel Results => _workspace.Results;
    /// <summary>
    /// 初始化 ToolCardViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public ToolCardViewModel(IExecutableTool tool, Func<Vision.Core.Roi.SearchRegion?>? searchRegion = null)
        : this(tool, searchRegion, null) { }

    public ToolCardViewModel(IExecutableTool tool, Func<Vision.Core.Roi.SearchRegion?>? searchRegion, Action? prepareInput)
    {
        _workspace = new ToolCardWorkspace(tool, searchRegion, prepareInput);
        RunCommand = new AsyncRelayCommand(RunAsync, CanExecuteCommand);
        Model.PropertyChanged += OnModelChanged;
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName == nameof(Model.LastResult)) OnPropertyChanged(nameof(ResultDetails));
        if (e.PropertyName == nameof(Model.TableRows)) OnPropertyChanged(nameof(HasTabularResults));
        if (e.PropertyName is nameof(Model.IsBusy) or nameof(Model.IsReadOnly))
        {
            RefreshValidationCommand.NotifyCanExecuteChanged(); EnsureLoadedCommand.NotifyCanExecuteChanged(); RunCommand.NotifyCanExecuteChanged();
        }
    }
    public System.Data.DataView? TableRows => Model.TableRows;
    public bool HasTabularResults => TableRows != null;
    public string ResultDetails => Vision.Controls.Presentation.VisionDisplay.Details(LastResult);
    public Vision.Core.Results.ToolResult? LastResult => Model.LastResult;
    public void ClearResult() { if (!_disposed) _workspace.ClearResult(); }
    public string ToolName => Model.ToolName;
    public string StatusText { get => Model.StatusText; set => Model.StatusText = value; }
    public bool IsValid { get => Model.IsValid; set => Model.IsValid = value; }
    public long ElapsedMs { get => Model.ElapsedMs; set => Model.ElapsedMs = value; }
    public string ResultSummary { get => Model.ResultSummary; set => Model.ResultSummary = value; }
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetImage。
    /// 正文中的关键协作调用包括 CanUseCard、SetImage；具体条件和先后次序见方法体。
    /// </summary>
    public void SetImage(VisionImage? image) { if (!_disposed) _workspace.SetImage(image); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshValidation。
    /// 正文中的关键协作调用包括 nameof、CanUseCard、RefreshValidation；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteCommand))] public void RefreshValidation() { if (CanUseCard()) _workspace.RefreshValidation(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EnsureLoaded。
    /// 正文中的关键协作调用包括 nameof、CanUseCard、EnsureLoaded；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteCommand))] public void EnsureLoaded() { if (CanUseCard()) _workspace.EnsureLoaded(); }
    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 nameof、CanUseCard、Run；具体条件和先后次序见方法体。
    /// </summary>
    public void Run() { if (CanUseCard()) _workspace.Run(); }
    public Task RunAsync() => CanUseCard() ? _activeRun = _workspace.RunAsync() : Task.CompletedTask;

    // 包含命令及直接API发起的执行；关闭等待此任务，而不只查看命令的ExecutionTask。
    internal Task ExecutionTask => _activeRun ?? Task.CompletedTask;
    internal void BeginClose()
    {
        if (_disposed || _closing) return;
        _closing = true;
        RunCommand.NotifyCanExecuteChanged();
        RefreshValidationCommand.NotifyCanExecuteChanged();
        EnsureLoadedCommand.NotifyCanExecuteChanged();
        _workspace.ClearResult(); // 提升revision，正在返回的结果只释放、不再发布。
    }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 NotifyCanExecuteChanged、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.PropertyChanged -= OnModelChanged;
        RefreshValidationCommand.NotifyCanExecuteChanged();
        EnsureLoadedCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        _workspace.Dispose();
    }
}
