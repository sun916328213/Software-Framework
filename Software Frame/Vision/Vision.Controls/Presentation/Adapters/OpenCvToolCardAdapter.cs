using Vision.Controls;
using Vision.Core.Imaging;
using Vision.Core.Results;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>OpenCV Tool 适配器（VisionImage 直驱，可真实执行）。</summary>
public sealed class OpenCvToolCardAdapter<TParameters, TResult> : IExecutableTool
    where TParameters : class, new()
    where TResult : ToolResult, new()
{
    private readonly Vision.Tools.OpenCv.Internal.OpenCvToolBase<TParameters, TResult> _tool;
    public Vision.Core.Roi.ISearchRegionTool? SearchRegionTool => _tool as Vision.Core.Roi.ISearchRegionTool;
    public Vision.Core.Presentation.ILineSearchFixtureTool? LineSearchFixtureTool => _tool as Vision.Core.Presentation.ILineSearchFixtureTool;
    public Func<ToolResult>? PrepareBackgroundRun(VisionImage? image) =>
        (_tool as Vision.Core.Presentation.IBackgroundImageTool)?.PrepareRun(image);
    public Vision.Core.Presentation.ITemplateTool? TemplateTool => _tool as Vision.Core.Presentation.ITemplateTool;
    public ICropRegionTool? CropRegionTool => _tool as ICropRegionTool;
    public IReadOnlyList<NumericParameterDefinition> NumericInputs =>
        (_tool as Vision.Core.Presentation.INumericParameterTool)?.NumericInputs ?? Array.Empty<NumericParameterDefinition>();
    public Func<ToolResult>? PrepareBackgroundRun(VisionImage? image, NumericParameterValue? input) =>
        input == null ? PrepareBackgroundRun(image) :
        (_tool as Vision.Core.Presentation.INumericParameterTool)?.PrepareRun(image, input)
            ?? throw new NotSupportedException("此工具未声明数值参数消费能力。");
    private bool _disposed;

    /// <summary>包装一个 OpenCV Tool。</summary>
    public OpenCvToolCardAdapter(Vision.Tools.OpenCv.Internal.OpenCvToolBase<TParameters, TResult> tool)
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
        return _tool.Run(image);
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
