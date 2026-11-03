using Vision.Controls;
using Vision.Core.Imaging;
using Vision.Core.Results;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>HALCON Tool 适配器；公共图像由工具边界转换并在单次执行结束时释放。</summary>
public sealed class HalconToolCardAdapter<TParameters, TResult> : IExecutableTool
    where TParameters : class, new()
    where TResult : ToolResult, new()
{
    private readonly Vision.Tools.Halcon.Internal.HalconToolBase<TParameters, TResult> _tool;
    public Vision.Core.Roi.ISearchRegionTool? SearchRegionTool => _tool as Vision.Core.Roi.ISearchRegionTool;
    public Func<ToolResult>? PrepareBackgroundRun(VisionImage? image) =>
        (_tool as Vision.Core.Presentation.IBackgroundImageTool)?.PrepareRun(image);
    public Vision.Core.Presentation.ITemplateTool? TemplateTool => _tool as Vision.Core.Presentation.ITemplateTool;
    public ICropRegionTool? CropRegionTool => _tool as ICropRegionTool;
    public IReadOnlyList<LineParameterDefinition> LineInputs =>
        (_tool as Vision.Core.Presentation.ILineParameterTool)?.LineInputs ?? Array.Empty<LineParameterDefinition>();
    public Func<ToolResult> PrepareLineBackgroundRun(VisionImage? image, LineParameterValue input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return (_tool as Vision.Core.Presentation.ILineParameterTool)?.PrepareLineRun(image, input)
            ?? throw new NotSupportedException("此工具未声明线段消费能力。");
    }
    public IReadOnlyList<CircleParameterDefinition> CircleInputs =>
        (_tool as Vision.Core.Presentation.ICircleParameterTool)?.CircleInputs ?? Array.Empty<CircleParameterDefinition>();
    public Func<ToolResult> PrepareCircleBackgroundRun(VisionImage? image, CircleParameterValue input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return (_tool as Vision.Core.Presentation.ICircleParameterTool)?.PrepareCircleRun(image, input)
            ?? throw new NotSupportedException("此工具未声明圆消费能力。");
    }
    public IReadOnlyList<PointParameterDefinition> PointInputs =>
        (_tool as Vision.Core.Presentation.IPointParameterTool)?.PointInputs ?? Array.Empty<PointParameterDefinition>();
    public Func<ToolResult> PreparePointBackgroundRun(VisionImage? image, PointParameterValue input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return (_tool as Vision.Core.Presentation.IPointParameterTool)?.PreparePointRun(image, input)
            ?? throw new NotSupportedException("此工具未声明点消费能力。");
    }
    private bool _disposed;

    /// <summary>包装一个 HALCON Tool。</summary>
    public HalconToolCardAdapter(Vision.Tools.Halcon.Internal.HalconToolBase<TParameters, TResult> tool)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
    }

    /// <inheritdoc/>
    public string Name => _tool.Name;

    /// <inheritdoc/>
    public long LastElapsedMs => _tool.LastElapsedMs;

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Validate。
    /// 正文中的关键协作调用包括 ThrowIf、Validate；具体条件和先后次序见方法体。
    /// </remarks>
    public ValidationResult Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tool.Validate();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 ThrowIf、Run；具体条件和先后次序见方法体。
    /// </remarks>
    public ToolResult Run(VisionImage? image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tool.RunImage(image);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 EnsureLoaded。
    /// 正文中的关键协作调用包括 ThrowIf；具体条件和先后次序见方法体。
    /// </remarks>
    public void EnsureLoaded()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose、SuppressFinalize；具体条件和先后次序见方法体。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _tool.Dispose();
        GC.SuppressFinalize(this);
    }
}
