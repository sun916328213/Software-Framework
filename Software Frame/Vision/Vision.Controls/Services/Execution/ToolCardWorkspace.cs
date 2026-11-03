using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Controls.Presentation;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>单卡片拥有原工具、结果及表格；后台操作必须是工具显式准备的独立快照。</summary>
public sealed class ToolCardWorkspace : IDisposable
{
    private readonly IExecutableTool _tool;
    private readonly Func<SearchRegion?>? _searchRegion;
    private readonly Action? _prepareInput;
    private VisionImage? _image;
    private VisionResultTable? _table;
    private bool _disposed;
    private bool _running;
    private long _revision;
    public ToolCardModel Model { get; } = new();
    public ResultSelectionViewModel Results { get; } = new();
    public bool SupportsSearchRegion => _tool.SearchRegionTool != null;
    public IReadOnlyList<NumericParameterDefinition> NumericInputs => _tool.NumericInputs;
    public NumericParameterValue? ExecutionNumericParameter { get; set; }
    public IReadOnlyList<LineParameterDefinition> LineInputs => _tool.LineInputs;
    public LineParameterValue? ExecutionLineParameter { get; set; }
    public IReadOnlyList<CircleParameterDefinition> CircleInputs => _tool.CircleInputs;
    public CircleParameterValue? ExecutionCircleParameter { get; set; }
    public IReadOnlyList<PointParameterDefinition> PointInputs => _tool.PointInputs;
    public PointParameterValue? ExecutionPointParameter { get; set; }
    internal (Vision.Core.Geometry.Pose2D Reference, Vision.Core.Geometry.Pose2D? Current)? ExecutionSearchFixture { get; set; }

    public ToolCardWorkspace(IExecutableTool tool, Func<SearchRegion?>? searchRegion = null)
        : this(tool, searchRegion, null) { }

    public ToolCardWorkspace(IExecutableTool tool, Func<SearchRegion?>? searchRegion, Action? prepareInput)
    {
        _prepareInput = prepareInput;
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _searchRegion = searchRegion;
        Model.ToolName = tool.Name; Model.StatusText = "未校验"; Model.ResultSummary = "—";
    }
    public void SetImage(VisionImage? image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _image = image;
        ClearResult();
    }
    public void ClearResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ResetResult();
    }
    private void ResetResult()
    {
        _revision++;
        ExecutionNumericParameter = null;
        ExecutionLineParameter = null;
        ExecutionCircleParameter = null;
        ExecutionPointParameter = null;
        ExecutionSearchFixture = null;
        var previous = Model.LastResult;
        var previousTable = _table;
        _table = null;
        try
        {
            Model.ParameterValidation = null;
            Model.TableRows = null;
            Results.SetResult(null);
            Model.LastResult = null;
            Model.ResultSummary = "—"; Model.ElapsedMs = 0;
            Model.StatusText = Model.IsBusy ? "输入/ROI/参数已变化，本次结果将丢弃" : "输入或参数已变化，请重新校验并执行";
        }
        finally
        {
            try { previousTable?.Dispose(); }
            finally { previous?.Dispose(); }
        }
    }
    public void RefreshValidation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        var validation = _tool.Validate();
        Model.ParameterValidation = validation;
        Model.IsValid = validation.IsValid;
        Model.StatusText = validation.IsValid ? "校验通过" : $"校验失败：{validation.ToMessage()}";
    }
    public void EnsureLoaded()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        try { ClearResult(); _tool.EnsureLoaded(); Model.StatusText = "就绪"; }
        catch (Exception error) { Model.IsValid = false; Model.StatusText = $"加载失败：{error.Message}"; }
    }
    private void PrepareSearchRegion()
    {
        if (_searchRegion != null && _tool.SearchRegionTool is { } target) target.SearchRegion = _searchRegion();
    }
    private Func<ToolResult>? PrepareExecution()
    {
        var count = (ExecutionNumericParameter != null ? 1 : 0) + (ExecutionLineParameter != null ? 1 : 0) + (ExecutionCircleParameter != null ? 1 : 0) + (ExecutionPointParameter != null ? 1 : 0);
        if (count > 1) throw new NotSupportedException("当前不支持混合多个参数覆盖槽。");
        if (_tool.LineSearchFixtureTool is { } line)
        {
            if (count > 0) throw new NotSupportedException("线搜索定位不支持混入其他参数覆盖槽。");
            return line.PrepareLineSearchRun(_image, ExecutionSearchFixture?.Reference ?? default, ExecutionSearchFixture?.Current);
        }
        if (_tool.SearchRegionTool is Vision.Core.Presentation.ISearchRegionFixtureTool regionTool)
        {
            if (count > 0) throw new NotSupportedException("搜索域定位不支持混入其他参数覆盖槽。");
            return regionTool.PrepareSearchRegionRun(_image, regionTool.SearchRegion, ExecutionSearchFixture?.Reference ?? default, ExecutionSearchFixture?.Current);
        }
        if (ExecutionPointParameter != null) return _tool.PreparePointBackgroundRun(_image, ExecutionPointParameter);
        if (ExecutionCircleParameter != null) return _tool.PrepareCircleBackgroundRun(_image, ExecutionCircleParameter);
        if (ExecutionLineParameter != null) return _tool.PrepareLineBackgroundRun(_image, ExecutionLineParameter);
        return _tool.PrepareBackgroundRun(_image, ExecutionNumericParameter);
    }
    /// <summary>兼容同步 API；UI 执行命令走 RunAsync。</summary>
    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        _running = true;
        try
        {
            Model.IsBusy = true;
            ClearResult(); _prepareInput?.Invoke(); PrepareSearchRegion();
            var execution = ExecutionNumericParameter == null && ExecutionLineParameter == null && ExecutionCircleParameter == null && ExecutionPointParameter == null && ExecutionSearchFixture == null ? null : PrepareExecution();
            Publish(execution == null ? _tool.Run(_image) : execution());
        }
        catch (Exception error) { Model.ResultSummary = "—"; Model.StatusText = $"执行异常：{error.Message}"; }
        finally { _running = false; Model.IsBusy = false; }
    }
    /// <summary>延续调用者 UI 上下文发布。没有后台能力的工具保持原同步契约，不盲目跨 SDK 线程。</summary>
    public async Task RunAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        _running = true;
        var revision = _revision;
        var prepared = false;
        try
        {
            Model.IsBusy = true;
            ClearResult();
            Model.StatusText = "准备执行…";
            _prepareInput?.Invoke();
            PrepareSearchRegion();
            var execution = PrepareExecution();
            revision = _revision; prepared = true;
            Model.StatusText = execution == null ? "执行中…（此工具为同步执行）" : "执行中…（画布仍可平移/缩放）";
            var result = execution == null ? _tool.Run(_image) : await Task.Run(execution);
            if (_disposed || revision != _revision) { result.Dispose(); return; }
            Publish(result);
        }
        catch (Exception error)
        {
            if (!_disposed && (!prepared || revision == _revision))
            { Model.ResultSummary = "—"; Model.StatusText = $"执行异常：{error.Message}"; }
        }
        finally { _running = false; Model.IsBusy = false; }
    }
    private void Publish(ToolResult result)
    {
        Model.ParameterValidation = result.Validation;
        Model.LastResult = result; // 从这里开始本卡片拥有结果，即使展示适配失败也会释放。
        Results.SetResult(result);
        Model.IsValid = result.Validation?.IsValid ?? result.Success;
        Model.ElapsedMs = result.ElapsedMs;
        Model.ResultSummary = ToolResultText.Summarize(result);
        Model.StatusText = result.Success ? "执行成功" : $"执行失败：{result.Message}";
        try { _table = VisionResultTable.Create(result); Model.TableRows = _table?.View; }
        catch (Exception error) { Model.StatusText += $"；结果表不可用：{error.Message}"; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _image = null;
        // 后台闭包已复制数据并独立拥有 SDK，绝不继续访问这个已释放原工具。
        try { ResetResult(); }
        finally { _tool.Dispose(); }
    }
}
